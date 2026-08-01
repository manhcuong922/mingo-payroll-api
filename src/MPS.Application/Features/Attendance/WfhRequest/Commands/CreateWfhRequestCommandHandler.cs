using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Notifications;
using MPS.Application.Features.Attendance.Common;
using MPS.Application.Features.Attendance.WfhRequest.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Enums.Notifications;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using WfhRequestEntity = MPS.Domain.Entities.Attendance.WfhRequest;

namespace MPS.Application.Features.Attendance.WfhRequest.Commands;

public class CreateWfhRequestCommandHandler : IRequestHandler<CreateWfhRequestCommand, int>
{
    private const int DefaultMinNoticeDays = 1;
    private const decimal DefaultMaxDaysPerRequest = 30m;
    private const int DefaultWorkPlanMinLength = 20;

    private readonly IWfhRequestRepository _repository;
    private readonly IEmployeeShiftAssignmentRepository _shiftAssignmentRepository;
    private readonly IAttendanceSettingRepository _settingRepository;
    private readonly IAttendanceEditLockChecker _editLockChecker;
    private readonly WfhQuotaChecker _quotaChecker;
    private readonly AttendanceRequestDayConflictChecker _dayConflictChecker;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;
    private readonly IAttendanceApproverResolver _approverResolver;

    public CreateWfhRequestCommandHandler(
        IWfhRequestRepository repository,
        IEmployeeShiftAssignmentRepository shiftAssignmentRepository,
        IAttendanceSettingRepository settingRepository,
        IAttendanceEditLockChecker editLockChecker,
        WfhQuotaChecker quotaChecker,
        AttendanceRequestDayConflictChecker dayConflictChecker,
        IContractLookupReader lookup,
        ICurrentUserService currentUser,
        IClock clock,
        IPublisher publisher,
        IAttendanceApproverResolver approverResolver)
    {
        _repository = repository;
        _shiftAssignmentRepository = shiftAssignmentRepository;
        _settingRepository = settingRepository;
        _editLockChecker = editLockChecker;
        _quotaChecker = quotaChecker;
        _dayConflictChecker = dayConflictChecker;
        _lookup = lookup;
        _currentUser = currentUser;
        _clock = clock;
        _publisher = publisher;
        _approverResolver = approverResolver;
    }

    public async Task<int> Handle(CreateWfhRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        if (request.EndDate < request.StartDate)
            throw new DomainValidationException("EndDate must be on/after StartDate.");

        var setting = await _settingRepository.GetOrCreateDefaultAsync(_clock.UtcNow, cancellationToken);
        var minNoticeDays = setting.WfhMinNoticeDays ?? DefaultMinNoticeDays;
        var maxDaysPerRequest = setting.WfhMaxDaysPerRequest ?? DefaultMaxDaysPerRequest;
        var workPlanMinLength = setting.WfhWorkPlanMinLength ?? DefaultWorkPlanMinLength;

        var today = DateOnly.FromDateTime(_clock.UtcNow.AddHours(7));
        var daysAhead = request.StartDate.DayNumber - today.DayNumber;
        if (daysAhead < minNoticeDays && string.IsNullOrWhiteSpace(request.OverrideReason))
        {
            throw new DomainValidationException(
                $"WFH phải nộp trước ít nhất {minNoticeDays} ngày. Hiện chỉ báo trước {daysAhead} ngày.");
        }

        var workPlan = request.WorkPlan?.Trim() ?? string.Empty;
        if (workPlan.Length < workPlanMinLength)
            throw new DomainValidationException(
                $"WorkPlan phải mô tả tối thiểu {workPlanMinLength} ký tự.");

        // Đếm ngày làm việc trong khoảng: mỗi ngày phải có shift assignment.
        var totalDays = await CountWorkDaysWithShiftAsync(employeeId, request.StartDate, request.EndDate, cancellationToken);
        if (totalDays <= 0m)
            throw new DomainValidationException("Không có ngày làm việc nào (có phân ca) trong khoảng WFH.");

        if (totalDays > maxDaysPerRequest)
            throw new DomainValidationException(
                $"Đơn WFH tối đa {maxDaysPerRequest:0.#} ngày/lần; yêu cầu {totalDays:0.#}.");

        await _editLockChecker.EnsureCanModifyAsync(request.StartDate, request.OverrideReason, cancellationToken);

        // [D8] — chặn CHÉO LOẠI: cả WFH↔WFH, WFH↔LateEarly, WFH↔Nghỉ (Pending/Approved), WFH↔OT.
        await _dayConflictChecker.EnsureNoConflictAsync(
            employeeId,
            request.StartDate,
            request.EndDate,
            AttendanceRequestKind.Wfh,
            excludeRequestId: null,
            cancellationToken);

        await _quotaChecker.EnsureWithinQuotaAsync(
            employeeId, request.StartDate, request.EndDate, totalDays, _clock.UtcNow, cancellationToken);

        var resolvedRoute = ApproverRouteResolver.Resolve(request.ApproverRoute, _currentUser.RoleId);

        var entity = new WfhRequestEntity
        {
            EmployeeInformationId = employeeId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TotalDays = totalDays,
            WorkPlan = workPlan,
            ApproverRoute = resolvedRoute,
            Status = WfhRequestStatus.Pending,
            Reason = request.Reason,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId
        };

        var requestId = await _repository.AddAsync(entity, cancellationToken);

        var approvers = await _approverResolver.ResolveForSubmitAsync(
            AttendanceRequestKind.Wfh, resolvedRoute, employeeId,
            tierOneApproverUserId: null, excludeUserId: userId, cancellationToken);

        await _publisher.Publish(new AttendanceRequestSubmittedNotification(
            Kind: AttendanceRequestKind.Wfh,
            RequestId: requestId,
            SubmittedByUserId: userId,
            RequesterUserId: userId,
            ApproverUserIds: approvers,
            Summary: $"WFH {totalDays:0.#} ngày từ {request.StartDate:dd/MM/yyyy} tới {request.EndDate:dd/MM/yyyy}"),
            cancellationToken);

        return requestId;
    }

    private async Task<decimal> CountWorkDaysWithShiftAsync(
        int employeeId,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        var count = 0;
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var assignments = await _shiftAssignmentRepository.GetByEmployeeAndDateAsync(employeeId, date, cancellationToken);
            if (assignments.Count > 0) count++;
        }
        return count;
    }
}
