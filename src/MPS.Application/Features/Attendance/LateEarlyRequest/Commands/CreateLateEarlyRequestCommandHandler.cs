using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Notifications;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Enums.Notifications;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using LateEarlyRequestEntity = MPS.Domain.Entities.Attendance.LateEarlyRequest;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Commands;

public class CreateLateEarlyRequestCommandHandler : IRequestHandler<CreateLateEarlyRequestCommand, int>
{
    private const int DefaultBackdateDaysAllowed = 2;

    private readonly ILateEarlyRequestRepository _repository;
    private readonly IEmployeeShiftAssignmentRepository _shiftAssignmentRepository;
    private readonly IAttendanceSettingRepository _settingRepository;
    private readonly IAttendanceEditLockChecker _editLockChecker;
    private readonly AttendanceRequestDayConflictChecker _dayConflictChecker;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;
    private readonly IAttendanceApproverResolver _approverResolver;

    public CreateLateEarlyRequestCommandHandler(
        ILateEarlyRequestRepository repository,
        IEmployeeShiftAssignmentRepository shiftAssignmentRepository,
        IAttendanceSettingRepository settingRepository,
        IAttendanceEditLockChecker editLockChecker,
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
        _dayConflictChecker = dayConflictChecker;
        _lookup = lookup;
        _currentUser = currentUser;
        _clock = clock;
        _publisher = publisher;
        _approverResolver = approverResolver;
    }

    public async Task<int> Handle(CreateLateEarlyRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        // Employee phải có shift assignment cho WorkDate — nếu không có = ngày nghỉ, không cần đơn.
        var assignments = await _shiftAssignmentRepository.GetByEmployeeAndDateAsync(
            employeeId, request.WorkDate, cancellationToken);
        if (assignments.Count == 0)
            throw new DomainValidationException(
                $"Ngày {request.WorkDate:dd/MM/yyyy} không có phân ca — không cần đơn đi trễ/về sớm.");

        // ⭐ LATE-EARLY-2026-08 — trần số phút không còn là hằng số 240 mà là CHÍNH CA ĐƯỢC PHÂN của
        // ngày đó (quyết định PO 24/08/2026: bỏ quota số lần/tháng, đổi lại đơn không được vượt ra
        // ngoài phạm vi ca). Ca sáng 4h ⇒ tối đa 240'; ca fulltime 8h ⇒ 480'; NV có 2 ca trong ngày
        // ⇒ cộng cả hai. Ca ĐÊM vắt sang hôm sau vẫn tính trọn độ dài ca ở đây vì đơn luôn gắn ngày
        // BẮT ĐẦU ca — không cần (và không được) nộp thêm đơn cho ngày hôm sau.
        //
        // Mẫu số là `StandardHours` (đã trừ nghỉ giữa ca), cùng đại lượng mà `PayrollDataCollector`
        // dùng làm mẫu số chia công — để "xin tối đa" luôn khớp đúng một ca công, không lệch vì giờ
        // nghỉ trưa.
        var assignedMinutes = (int)decimal.Round(
            assignments.Sum(a => a.ShiftTemplate.StandardHours) * 60m, 0, MidpointRounding.AwayFromZero);
        if (assignedMinutes <= 0)
            throw new DomainValidationException(
                $"Ca được phân ngày {request.WorkDate:dd/MM/yyyy} khai giờ chuẩn ≤ 0 — không xác định được "
                + "giới hạn đơn đi trễ/về sớm. Kiểm tra lại cấu hình ca.");
        if (request.Minutes > assignedMinutes)
            throw new DomainValidationException(
                $"Số phút xin ({request.Minutes}) vượt thời lượng ca được phân ngày "
                + $"{request.WorkDate:dd/MM/yyyy} ({assignedMinutes} phút). Nghỉ trọn ca thì nộp đơn nghỉ phép.");

        var today = DateOnly.FromDateTime(_clock.UtcNow.AddHours(7));
        var isUnplanned = request.WorkDate <= today;

        if (isUnplanned)
        {
            var setting = await _settingRepository.GetOrCreateDefaultAsync(_clock.UtcNow, cancellationToken);
            var backdateDaysAllowed = setting.LateEarlyBackdateDaysAllowed ?? DefaultBackdateDaysAllowed;
            var daysBehind = today.DayNumber - request.WorkDate.DayNumber;
            if (daysBehind > backdateDaysAllowed && string.IsNullOrWhiteSpace(request.OverrideReason))
            {
                throw new DomainValidationException(
                    $"Đơn đột xuất cho ngày {request.WorkDate:dd/MM/yyyy} vượt hạn nộp trễ ({backdateDaysAllowed} ngày).");
            }
        }

        await _editLockChecker.EnsureCanModifyAsync(request.WorkDate, request.OverrideReason, cancellationToken);

        // [D8] — chặn CHÉO LOẠI: 1 ngày đã có đơn nghỉ / WFH / đi muộn / OT nào thì không nhận thêm.
        await _dayConflictChecker.EnsureNoConflictAsync(
            employeeId,
            request.WorkDate,
            request.WorkDate,
            AttendanceRequestKind.LateEarly,
            excludeRequestId: null,
            cancellationToken);

        var resolvedRoute = ApproverRouteResolver.Resolve(request.ApproverRoute, _currentUser.RoleId);

        var entity = new LateEarlyRequestEntity
        {
            EmployeeInformationId = employeeId,
            WorkDate = request.WorkDate,
            Kind = request.Kind,
            Minutes = request.Minutes,
            IsUnplanned = isUnplanned,
            DeductFromLeaveBalance = false,
            ApproverRoute = resolvedRoute,
            Status = LateEarlyRequestStatus.Pending,
            Reason = request.Reason,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId
        };

        var requestId = await _repository.AddAsync(entity, cancellationToken);

        var approvers = await _approverResolver.ResolveForSubmitAsync(
            AttendanceRequestKind.LateEarly, resolvedRoute, employeeId,
            tierOneApproverUserId: null, excludeUserId: userId, cancellationToken);

        await _publisher.Publish(new AttendanceRequestSubmittedNotification(
            Kind: AttendanceRequestKind.LateEarly,
            RequestId: requestId,
            SubmittedByUserId: userId,
            RequesterUserId: userId,
            ApproverUserIds: approvers,
            Summary: $"{request.Kind} {request.Minutes} phút ngày {request.WorkDate:dd/MM/yyyy}"),
            cancellationToken);

        return requestId;
    }
}
