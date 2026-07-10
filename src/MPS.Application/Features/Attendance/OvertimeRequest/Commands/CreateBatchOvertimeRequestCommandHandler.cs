using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using OvertimeRequestEntity = MPS.Domain.Entities.Attendance.OvertimeRequest;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

/// <summary>
/// Handler tạo batch OT — theo overtime-request-workflow-plan.md §5.2.
///
/// Flow:
///  1. Resolve currentUser → PM → EmployeeInformation → DepartmentId
///  2. Load Project → validate exists, Active, thuộc dept của PM (loose scope)
///  3. Snapshot LaborLawConstantsVersion active tại WorkDate → khoá rate cho composer
///  4. Load N EmployeeInformation → mỗi NV: validate thuộc dept của Project
///  5. Với mỗi NV: OvertimeCapChecker + split hours + tạo OvertimeRequest
///  6. Partial success — 1 NV fail không rollback cả batch (Q6)
/// </summary>
public class CreateBatchOvertimeRequestCommandHandler
    : IRequestHandler<CreateBatchOvertimeRequestCommand, CreateBatchOvertimeRequestResult>
{
    private readonly IOvertimeRequestRepository _repository;
    private readonly IProjectRepository _projectRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IContractLookupReader _lookup;
    private readonly OvertimeCapChecker _capChecker;
    private readonly IWorkHourSplitter _splitter;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CreateBatchOvertimeRequestCommandHandler(
        IOvertimeRequestRepository repository,
        IProjectRepository projectRepository,
        IEmployeeInformationRepository employeeRepository,
        IContractLookupReader lookup,
        OvertimeCapChecker capChecker,
        IWorkHourSplitter splitter,
        IComplianceLookupService complianceLookup,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _projectRepository = projectRepository;
        _employeeRepository = employeeRepository;
        _lookup = lookup;
        _capChecker = capChecker;
        _splitter = splitter;
        _complianceLookup = complianceLookup;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<CreateBatchOvertimeRequestResult> Handle(
        CreateBatchOvertimeRequestCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve PM → Department
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var pmEmployeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");
        var pmEmployee = await _employeeRepository.GetByIdAsync(pmEmployeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee {pmEmployeeId} not found.");
        if (pmEmployee.DepartmentId is not int pmDepartmentId)
            throw new DomainValidationException("PM chưa gắn với phòng ban — không thể tạo OT.");

        // 2. Load Project → validate
        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Project {request.ProjectId} not found.");
        // Chỉ tổ trưởng phụ trách chính đơn này (ResponsibleUserId) mới giao được OT.
        // PM đơn khác dù cùng xưởng cũng không được giao OT cho NV của đơn này.
        if (project.ResponsibleUserId != userId)
            throw new ForbiddenAccessException("Bạn không phải tổ trưởng phụ trách đơn này.");
        // Guard defensive: PM phải cùng xưởng với project — thường đảm bảo bởi flow set ResponsibleUser,
        // nhưng vẫn check để phòng khi dữ liệu bị lệch (PM đổi xưởng sau khi được gán).
        if (project.DepartmentId != pmDepartmentId)
            throw new ForbiddenAccessException("PM không thuộc phòng ban của project này.");

        // 3. Multiplier resolution: composer sẽ compute từ công thức + LaborLawConstantsVersion
        //    snapshotted trên entity. PM chỉ nhập OtStartTime/OtEndTime.

        // 4. Load N NV
        var employees = new List<Domain.Entities.EmployeeInformation>(request.EmployeeInformationIds.Count);
        foreach (var empId in request.EmployeeInformationIds.Distinct())
        {
            var emp = await _employeeRepository.GetByIdAsync(empId, cancellationToken);
            if (emp is not null) employees.Add(emp);
        }
        if (employees.Count == 0)
            throw new DomainValidationException("Danh sách nhân viên rỗng hoặc không hợp lệ.");

        // 5. Prep — labor law + hours + batchId
        var laborLaw = await _complianceLookup.GetLaborLawConstantsAtAsync(request.WorkDate, cancellationToken);
        var otHours = CalculateHours(request.OtStartTime, request.OtEndTime);
        var batchId = Guid.NewGuid();
        var now = _clock.UtcNow;

        // 6. Per-NV loop — partial success
        var results = new List<CreateBatchItemResult>(employees.Count);
        foreach (var emp in employees)
        {
            // Scope check: NV phải thuộc dept của project (loose)
            if (emp.DepartmentId != project.DepartmentId)
            {
                results.Add(new CreateBatchItemResult(
                    emp.Id, emp.FullName, Success: false, RequestId: null,
                    Error: $"NV không thuộc phòng ban {project.DepartmentId} của project."));
                continue;
            }

            try
            {
                // Guard trùng OT: NV đã có Pending/Approved giao thoa khung giờ trên ngày này thì skip.
                // Bảo vệ khỏi PM bấm "Giao OT" nhiều lần cho cùng 1 NV/ngày/khung giờ.
                var existingSameDay = await _repository.GetActiveOnDateForEmployeeAsync(
                    emp.Id, request.WorkDate, excludeId: null, cancellationToken);
                var conflict = existingSameDay.FirstOrDefault(e => OvertimeTimeOverlap.Overlaps(
                    request.WorkDate, request.OtStartTime, request.OtEndTime,
                    e.WorkDate, e.OtStartTime, e.OtEndTime));
                if (conflict is not null)
                {
                    throw new DomainValidationException(
                        $"NV đã có đơn OT #{conflict.Id} ({conflict.OtStartTime:HH\\:mm}–{conflict.OtEndTime:HH\\:mm}, {conflict.Status}) trên ngày này giao thoa khung giờ mới.");
                }

                await _capChecker.CheckAsync(
                    emp.Id, request.WorkDate, request.OtStartTime, request.OtEndTime, otHours, cancellationToken);

                var breakdown = _splitter.Split(new WorkHourSplitInput(
                    CheckIn: request.OtStartTime,
                    CheckOut: request.OtEndTime,
                    BreakStart: null,
                    BreakEnd: null,
                    NightStart: laborLaw.NightStartTime,
                    NightEnd: laborLaw.NightEndTime));

                var entity = new OvertimeRequestEntity
                {
                    EmployeeInformationId = emp.Id,
                    ProjectId = project.Id,
                    ApproverRoute = request.ApproverRoute,
                    BatchId = batchId,
                    WorkDate = request.WorkDate,
                    OtStartTime = request.OtStartTime,
                    OtEndTime = request.OtEndTime,
                    OtHours = otHours,
                    LaborLawConstantsVersionId = laborLaw.VersionId, // Khoá rate — composer dùng version này
                    OtDayHours = breakdown.DayHours,
                    OtNightHours = breakdown.NightHours,
                    Status = OtRequestStatus.Pending,
                    Reason = request.Reason,
                    CreatedAt = now,
                    CreatedByUserId = userId,
                };

                var newId = await _repository.AddAsync(entity, cancellationToken);
                results.Add(new CreateBatchItemResult(
                    emp.Id, emp.FullName, Success: true, RequestId: newId, Error: null));
            }
            catch (DomainValidationException ex)
            {
                results.Add(new CreateBatchItemResult(
                    emp.Id, emp.FullName, Success: false, RequestId: null, Error: ex.Message));
            }
        }

        return new CreateBatchOvertimeRequestResult(
            BatchId: batchId,
            SuccessCount: results.Count(r => r.Success),
            FailureCount: results.Count(r => !r.Success),
            Results: results);
    }

    private static decimal CalculateHours(TimeOnly start, TimeOnly end)
    {
        var spanMinutes = end > start
            ? (end - start).TotalMinutes
            : (24 * 60 - (start - TimeOnly.MinValue).TotalMinutes) + (end - TimeOnly.MinValue).TotalMinutes;
        return Math.Round((decimal)spanMinutes / 60m, 2);
    }
}
