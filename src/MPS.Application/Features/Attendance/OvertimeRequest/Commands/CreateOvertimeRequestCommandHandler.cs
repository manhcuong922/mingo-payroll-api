using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Enums;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using OvertimeRequestEntity = MPS.Domain.Entities.Attendance.OvertimeRequest;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

/// <summary>
/// Handler cho <see cref="CreateOvertimeRequestCommand"/> — luồng OT v3 §5.2.
///
/// Flow:
///  1. Resolve currentUser → EmployeeInformation
///  2. Load Project → validate exists, Active, không bị soft-delete/complete,
///     có ResponsibleUserId (=PM tổ trưởng đơn, không được null)
///  3. Validate NV có ≥1 task Status=InProgress trong đơn này
///  4. Guard trùng OT: NV đã có Pending/Approved giao thoa khung giờ trên ngày này
///  5. Snapshot LaborLawConstantsVersion để khoá rate cho payroll composer
///  6. Cap check + split day/night hours
///  7. Insert Status=Pending (chờ PM review)
/// </summary>
public class CreateOvertimeRequestCommandHandler : IRequestHandler<CreateOvertimeRequestCommand, int>
{
    private readonly IOvertimeRequestRepository _repository;
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectTaskRepository _taskRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IContractLookupReader _lookup;
    private readonly OvertimeCapChecker _capChecker;
    private readonly IWorkHourSplitter _splitter;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IRequestDayOccupancyReader _dayOccupancy;

    public CreateOvertimeRequestCommandHandler(
        IOvertimeRequestRepository repository,
        IProjectRepository projectRepository,
        IProjectTaskRepository taskRepository,
        IEmployeeInformationRepository employeeRepository,
        IContractLookupReader lookup,
        OvertimeCapChecker capChecker,
        IWorkHourSplitter splitter,
        IComplianceLookupService complianceLookup,
        ICurrentUserService currentUser,
        IClock clock,
        IRequestDayOccupancyReader dayOccupancy)
    {
        _dayOccupancy = dayOccupancy;
        _repository = repository;
        _projectRepository = projectRepository;
        _taskRepository = taskRepository;
        _employeeRepository = employeeRepository;
        _lookup = lookup;
        _capChecker = capChecker;
        _splitter = splitter;
        _complianceLookup = complianceLookup;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<int> Handle(CreateOvertimeRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        // 1. Resolve NV
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");
        var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee {employeeId} not found.");

        // 2. Load & validate Project (đơn sản xuất)
        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Project {request.ProjectId} not found.");
        if (project.DeletedAt is not null)
            throw new DomainValidationException("Đơn sản xuất đã bị huỷ, không thể đăng ký OT.");
        if (project.CompletedAt is not null)
            throw new DomainValidationException("Đơn sản xuất đã hoàn thành, không thể đăng ký OT.");
        if (project.Status != ProjectStatus.Active)
            throw new DomainValidationException($"Đơn sản xuất đang ở trạng thái {project.Status}, không thể đăng ký OT.");
        if (project.ResponsibleUserId is null)
            throw new DomainValidationException("Đơn sản xuất chưa có tổ trưởng phụ trách — liên hệ quản lý xưởng để gán trước khi đăng ký OT.");

        // 3. Validate NV được assign task nào trong đơn này (Q18: không kiểm Status).
        var hasAssignedTask = await _taskRepository.HasAssignedTaskInProjectAsync(
            userId, request.ProjectId, cancellationToken);
        if (!hasAssignedTask)
            throw new DomainValidationException(
                "Bạn không được assign task nào trong đơn này — không đủ điều kiện đăng ký OT.");

        // 4. Prep — labor law + hours
        var laborLaw = await _complianceLookup.GetLaborLawConstantsAtAsync(request.WorkDate, cancellationToken);
        var otHours = CalculateHours(request.OtStartTime, request.OtEndTime);

        // 5. Guard trùng OT trên ngày
        var existingSameDay = await _repository.GetActiveOnDateForEmployeeAsync(
            employeeId, request.WorkDate, excludeId: null, cancellationToken);
        var conflict = existingSameDay.FirstOrDefault(e => OvertimeTimeOverlap.Overlaps(
            request.WorkDate, request.OtStartTime, request.OtEndTime,
            e.WorkDate, e.OtStartTime, e.OtEndTime));
        if (conflict is not null)
        {
            throw new DomainValidationException(
                $"Bạn đã có đơn OT #{conflict.Id} ({conflict.OtStartTime:HH\\:mm}–{conflict.OtEndTime:HH\\:mm}, {conflict.Status}) giao thoa khung giờ này.");
        }

        // 5b. [D8] — không đăng ký OT cho ngày đang nghỉ phép (đơn đã duyệt): hôm đó NV không đi làm
        // nên không thể có giờ làm thêm. WFH / đi muộn-về sớm KHÔNG chặn (vẫn làm việc trong ngày).
        await RequestDayGuard.EnsureNotOnApprovedLeaveAsync(
            _dayOccupancy, employeeId, request.WorkDate, request.WorkDate, cancellationToken);

        // 6. Cap check + split
        await _capChecker.CheckAsync(
            employeeId, request.WorkDate, request.OtStartTime, request.OtEndTime, otHours, cancellationToken);

        var breakdown = _splitter.Split(new WorkHourSplitInput(
            CheckIn: request.OtStartTime,
            CheckOut: request.OtEndTime,
            BreakStart: null,
            BreakEnd: null,
            NightStart: laborLaw.NightStartTime,
            NightEnd: laborLaw.NightEndTime));

        // 7. Insert Status=Pending
        var now = _clock.UtcNow;
        var entity = new OvertimeRequestEntity
        {
            EmployeeInformationId = employeeId,
            ProjectId = project.Id,
            ApproverRoute = null,           // v3: PM sẽ set khi Forward
            BatchId = null,                 // v3: không dùng batch nữa
            WorkDate = request.WorkDate,
            OtStartTime = request.OtStartTime,
            OtEndTime = request.OtEndTime,
            OtHours = otHours,
            LaborLawConstantsVersionId = laborLaw.VersionId,
            OtDayHours = breakdown.DayHours,
            OtNightHours = breakdown.NightHours,
            Status = OtRequestStatus.Pending,
            Reason = request.Reason,
            CreatedAt = now,
            CreatedByUserId = userId,
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }

    private static decimal CalculateHours(TimeOnly start, TimeOnly end)
    {
        var spanMinutes = end > start
            ? (end - start).TotalMinutes
            : (24 * 60 - (start - TimeOnly.MinValue).TotalMinutes) + (end - TimeOnly.MinValue).TotalMinutes;
        return Math.Round((decimal)spanMinutes / 60m, 2);
    }
}
