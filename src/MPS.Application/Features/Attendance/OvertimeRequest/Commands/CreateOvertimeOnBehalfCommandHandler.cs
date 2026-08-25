using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Common;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Enums;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using OvertimeRequestEntity = MPS.Domain.Entities.Attendance.OvertimeRequest;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

/// <summary>
/// Case 6 — Tổ trưởng đăng ký OT hộ NV trong đơn mình phụ trách. Xem <see cref="CreateOvertimeOnBehalfCommand"/>.
///
/// Flow:
///  1. Load Project → Active, không deleted/completed, ResponsibleUserId == currentUser (tổ trưởng).
///  2. Load NV mục tiêu → phải có tài khoản (UserId).
///  3. Case 8: NV còn HĐ Active. Case 4: WorkDate không quá xa tương lai.
///  4. NV mục tiêu được assign task trong đơn.
///  5. Guard trùng OT + chéo loại (Leave/WFH/LateEarly) + cap.
///  6. Insert Status=PendingApprover (vào thẳng), ApproverRoute set sẵn, PmReviewedBy=tổ trưởng (BR-10).
/// </summary>
public class CreateOvertimeOnBehalfCommandHandler : IRequestHandler<CreateOvertimeOnBehalfCommand, int>
{
    private readonly IOvertimeRequestRepository _repository;
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectTaskRepository _taskRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IContractRepository _contracts;
    private readonly OvertimeCapChecker _capChecker;
    private readonly AttendanceRequestDayConflictChecker _dayConflictChecker;
    private readonly IWorkHourSplitter _splitter;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CreateOvertimeOnBehalfCommandHandler(
        IOvertimeRequestRepository repository,
        IProjectRepository projectRepository,
        IProjectTaskRepository taskRepository,
        IEmployeeInformationRepository employeeRepository,
        IContractRepository contracts,
        OvertimeCapChecker capChecker,
        AttendanceRequestDayConflictChecker dayConflictChecker,
        IWorkHourSplitter splitter,
        IComplianceLookupService complianceLookup,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _projectRepository = projectRepository;
        _taskRepository = taskRepository;
        _employeeRepository = employeeRepository;
        _contracts = contracts;
        _capChecker = capChecker;
        _dayConflictChecker = dayConflictChecker;
        _splitter = splitter;
        _complianceLookup = complianceLookup;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<int> Handle(CreateOvertimeOnBehalfCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        // 1. Project + gate tổ trưởng phụ trách.
        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Project {request.ProjectId} not found.");
        if (project.DeletedAt is not null)
            throw new DomainValidationException("Đơn sản xuất đã bị huỷ, không thể đăng ký OT.");
        if (project.CompletedAt is not null)
            throw new DomainValidationException("Đơn sản xuất đã hoàn thành, không thể đăng ký OT.");
        if (project.Status != ProjectStatus.Active)
            throw new DomainValidationException($"Đơn sản xuất đang ở trạng thái {project.Status}, không thể đăng ký OT.");
        if (project.ResponsibleUserId != callerUserId)
            throw new ForbiddenAccessException(
                "Bạn không phải tổ trưởng đơn này — không thể đăng ký OT hộ nhân viên.");

        // 2. NV mục tiêu — phải có tài khoản (task được giao theo user).
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeInformationId, cancellationToken)
            ?? throw new NotFoundException($"Employee {request.EmployeeInformationId} not found.");
        if (employee.UserId is not int targetUserId)
            throw new DomainValidationException("Nhân viên chưa có tài khoản — không thể đăng ký OT.");

        // 3. Case 8 + Case 4.
        if (!await _contracts.HasActiveContractAsync(request.EmployeeInformationId, cancellationToken))
            throw new DomainValidationException(
                "CONTRACT_NOT_ACTIVE: Nhân viên không còn hợp đồng lao động hiệu lực — không thể đăng ký OT.");

        // X5 — chặn cả đường ĐĂNG KÝ HỘ. Hai đường vào, chặn một đường là thủng: quản lý tạo hộ là
        // đúng cái đường mà đơn tăng ca của người khoán việc trong dữ liệu thật đã đi qua.
        OvertimeContractKindGuard.EnsureLaborRelationship(
            await _contracts.GetActiveContractKindsAsync(request.EmployeeInformationId, cancellationToken));
        OvertimeWorkDateGuard.EnsureNotTooFarInFuture(request.WorkDate, _clock.UtcNow);

        // 4. NV mục tiêu có task trong đơn.
        if (!await _taskRepository.HasAssignedTaskInProjectAsync(targetUserId, request.ProjectId, cancellationToken))
            throw new DomainValidationException(
                "Nhân viên không được assign task nào trong đơn này — không đủ điều kiện đăng ký OT.");

        var laborLaw = await _complianceLookup.GetLaborLawConstantsAtAsync(request.WorkDate, cancellationToken);
        var otHours = CalculateHours(request.OtStartTime, request.OtEndTime);

        // 5. Guard trùng OT — #41: quét D±1 để bắt OT vắt nửa đêm (đơn D 22:00→02:00 chồng giờ đơn D+1).
        var existingSameDay = await _repository.GetActiveInRangeForEmployeeAsync(
            request.EmployeeInformationId, request.WorkDate.AddDays(-1), request.WorkDate.AddDays(1), excludeId: null, cancellationToken);
        var conflict = existingSameDay.FirstOrDefault(e => OvertimeTimeOverlap.Overlaps(
            request.WorkDate, request.OtStartTime, request.OtEndTime,
            e.WorkDate, e.OtStartTime, e.OtEndTime));
        if (conflict is not null)
            throw new DomainValidationException(
                $"Nhân viên đã có đơn OT #{conflict.Id} ({conflict.OtStartTime:HH\\:mm}–{conflict.OtEndTime:HH\\:mm}, {conflict.Status}) giao thoa khung giờ này.");

        await _dayConflictChecker.EnsureNoConflictAsync(
            request.EmployeeInformationId, request.WorkDate, request.WorkDate,
            AttendanceRequestKind.Overtime, excludeRequestId: null, cancellationToken);

        await _capChecker.CheckAsync(
            request.EmployeeInformationId, request.WorkDate, request.OtStartTime, request.OtEndTime, otHours, cancellationToken);

        var breakdown = _splitter.Split(new WorkHourSplitInput(
            CheckIn: request.OtStartTime,
            CheckOut: request.OtEndTime,
            BreakStart: null,
            BreakEnd: null,
            NightStart: laborLaw.NightStartTime,
            NightEnd: laborLaw.NightEndTime));

        // 6. Insert — vào thẳng PendingApprover, ApproverRoute set sẵn; tổ trưởng đứng tên PM (BR-10).
        var now = _clock.UtcNow;
        var entity = new OvertimeRequestEntity
        {
            EmployeeInformationId = request.EmployeeInformationId,
            ProjectId = project.Id,
            ApproverRoute = request.ApproverRoute,
            WorkDate = request.WorkDate,
            OtStartTime = request.OtStartTime,
            OtEndTime = request.OtEndTime,
            OtHours = otHours,
            LaborLawConstantsVersionId = laborLaw.VersionId,
            OtDayHours = breakdown.DayHours,
            OtNightHours = breakdown.NightHours,
            Status = OtRequestStatus.PendingApprover,
            Reason = request.Reason,
            CreatedAt = now,
            CreatedByUserId = callerUserId,
            PmReviewedByUserId = callerUserId,
            PmReviewedAt = now,
            PmReviewNote = "Tổ trưởng đăng ký hộ (bỏ qua bước PM review).",
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
