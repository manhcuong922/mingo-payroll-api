using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Common;
using MPS.Application.Features.Attendance.LeaveRequest.Common;
using MPS.Application.Features.Attendance.LeaveType.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using LeaveRequestEntity = MPS.Domain.Entities.Attendance.LeaveRequest;
using LeaveTypeEntity = MPS.Domain.Entities.Attendance.LeaveType;

namespace MPS.Application.Features.Attendance.LeaveRequest.Commands;

/// <summary>
/// FT-13 — tạo yêu cầu nghỉ phép theo cấu hình LeaveType động.
/// Bổ sung: notice-period (SLA nội quy), retroactive gate (Sick/Maternity), auto-fallback UNPAID,
/// và cutoff enforcement khi StartDate không thuộc retroactive.
/// </summary>
public class CreateLeaveRequestCommandHandler : IRequestHandler<CreateLeaveRequestCommand, CreateLeaveRequestResult>
{
    private readonly ILeaveRequestRepository _repository;
    private readonly ILeaveTypeRepository _leaveTypeRepository;
    private readonly ILeaveBalanceRepository _balanceRepository;
    private readonly IContractLookupReader _lookup;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IEmployeeShiftAssignmentRepository _shiftAssignmentRepository;
    private readonly IAttendanceEditLockChecker _editLockChecker;
    private readonly LeaveBalanceFallbackResolver _fallbackResolver;
    private readonly AttendanceRequestDayConflictChecker _dayConflictChecker;
    private readonly MedicalCertificateUploader _certificateUploader;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CreateLeaveRequestCommandHandler(
        ILeaveRequestRepository repository,
        ILeaveTypeRepository leaveTypeRepository,
        ILeaveBalanceRepository balanceRepository,
        IContractLookupReader lookup,
        IEmployeeInformationRepository employeeRepository,
        IEmployeeShiftAssignmentRepository shiftAssignmentRepository,
        IAttendanceEditLockChecker editLockChecker,
        LeaveBalanceFallbackResolver fallbackResolver,
        AttendanceRequestDayConflictChecker dayConflictChecker,
        MedicalCertificateUploader certificateUploader,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _leaveTypeRepository = leaveTypeRepository;
        _balanceRepository = balanceRepository;
        _lookup = lookup;
        _employeeRepository = employeeRepository;
        _shiftAssignmentRepository = shiftAssignmentRepository;
        _editLockChecker = editLockChecker;
        _fallbackResolver = fallbackResolver;
        _dayConflictChecker = dayConflictChecker;
        _certificateUploader = certificateUploader;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<CreateLeaveRequestResult> Handle(CreateLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");
        var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee profile {employeeId} not found.");

        if (request.EndDate < request.StartDate)
            throw new DomainValidationException("EndDate must be on/after StartDate.");

        // [D8] — 1 ngày chỉ được 1 đơn Leave/WFH/LateEarly/OT. Kiểm SỚM, trước khi đụng quỹ phép.
        await _dayConflictChecker.EnsureNoConflictAsync(
            employeeId,
            request.StartDate,
            request.EndDate,
            AttendanceRequestKind.Leave,
            excludeRequestId: null,
            cancellationToken);

        var leaveType = await _leaveTypeRepository.GetByIdAsync(request.LeaveTypeId, cancellationToken)
            ?? throw new NotFoundException($"Leave type {request.LeaveTypeId} not found.");
        if (!leaveType.IsActive)
            throw new DomainValidationException($"Leave type '{leaveType.Code}' is inactive.");
        if (!leaveType.AllowEmployeeSelection)
            throw new DomainValidationException($"Leave type '{leaveType.Code}' is not available for employee self-service.");
        if (!LeaveTypeAssignmentPolicy.AppliesToEmployee(leaveType, employeeId, employee.DepartmentId))
            throw new DomainValidationException($"Leave type '{leaveType.Code}' is not assigned to this employee.");

        var today = DateOnly.FromDateTime(_clock.UtcNow.AddHours(7));
        var isRetroactive = request.StartDate < today;
        if (isRetroactive && !leaveType.AllowRetroactive)
            throw new DomainValidationException(
                $"Leave type '{leaveType.Code}' không cho phép nộp ngày đã qua.");

        var assignedDays = await GetShiftAssignedDaysAsync(employeeId, request.StartDate, request.EndDate, cancellationToken);
        var totalDays = (decimal)assignedDays.Count;
        if (totalDays <= 0)
            throw new DomainValidationException(
                "Khoảng nghỉ không có ngày nào đã được phân ca — vui lòng chọn ngày đi làm.");

        if (leaveType.MaxDaysPerRequest is decimal max && totalDays > max)
            throw new DomainValidationException(
                $"Leave type '{leaveType.Code}' allows at most {max} day(s) per request; requested {totalDays}.");

        if (!isRetroactive)
        {
            LeaveNoticePeriodChecker.EnsureCompliant(leaveType, request.StartDate, today, totalDays);
            await _editLockChecker.EnsureCanModifyAsync(request.StartDate, request.OverrideReason, cancellationToken);
        }

        await _dayConflictChecker.EnsureNoConflictAsync(
            employeeId,
            request.StartDate,
            request.EndDate,
            AttendanceRequestKind.Leave,
            excludeRequestId: null,
            cancellationToken);

        var finalType = leaveType;
        var switched = false;
        string? switchMessage = null;
        // Số ngày CÓ LƯƠNG tách ra khi xin vượt quỹ (`[D2]`). 0 = không tách.
        var paidSplitDays = 0;
        LeaveTypeEntity? unpaidType = null;

        if (LeaveTypeAssignmentPolicy.TracksYearlyBalance(leaveType))
        {
            var balance = await _balanceRepository.GetByKeyAsync(
                employeeId, request.StartDate.Year, leaveType.Id, cancellationToken);
            var yearlyQuota = LeaveTypeAssignmentPolicy.ResolveYearlyQuota(leaveType);
            if ((balance is null || balance.TotalDays <= 0m) && yearlyQuota > 0m)
            {
                await _balanceRepository.UpsertForLeaveTypeAsync(
                    leaveType.Id,
                    request.StartDate.Year,
                    new[] { employeeId },
                    yearlyQuota,
                    userId,
                    _clock.UtcNow,
                    cancellationToken);
                balance = await _balanceRepository.GetByKeyAsync(
                    employeeId, request.StartDate.Year, leaveType.Id, cancellationToken);
            }

            var remaining = (balance?.TotalDays ?? yearlyQuota) - (balance?.UsedDays ?? 0m);
            if (remaining < totalDays)
            {
                var fallback = await _fallbackResolver.ResolveFallbackAsync(
                    leaveType, employeeId, request.StartDate.Year, totalDays, cancellationToken);
                if (fallback is null)
                {
                    throw new DomainValidationException(
                        $"Insufficient '{leaveType.Code}' balance. Remaining={remaining}, requested={totalDays}.");
                }

                // `[D2]` — chỉ PHẦN VƯỢT mới là nghỉ không lương. Quỹ còn lại phải được dùng nốt,
                // không được bỏ phí (lỗi #37).
                //
                // Chỉ tách theo NGÀY TRỌN: bảng chấm công và `DayTypeResolver` phân loại từng ngày
                // là "nghỉ có lương" hay không, không có khái niệm nửa ngày. Quỹ lẻ (vd còn 2,5)
                // ⇒ 2 ngày có lương, 0,5 ngày còn lại giữ trong quỹ cho lần sau — không mất đi.
                var wholePaidDays = remaining > 0m ? (int)Math.Floor(remaining) : 0;
                paidSplitDays = Math.Min(wholePaidDays, assignedDays.Count - 1); // luôn chừa ≥1 ngày cho phần vượt

                if (paidSplitDays > 0)
                {
                    unpaidType = fallback;
                    switched = true;
                    switchMessage =
                        $"Quỹ '{leaveType.Code}' còn {remaining} ngày, đơn xin {totalDays} ngày. "
                        + $"Hệ thống tách đơn: {paidSplitDays} ngày đầu nghỉ có lương (dùng nốt quỹ), "
                        + $"{totalDays - paidSplitDays} ngày sau chuyển '{fallback.Code}' (nghỉ không lương).";
                }
                else
                {
                    // Không còn ngày trọn nào trong quỹ ⇒ cả đơn là nghỉ không lương (như trước).
                    finalType = fallback;
                    switched = true;
                    switchMessage = $"Đã hết quỹ '{leaveType.Code}' (còn {remaining}). "
                        + $"Hệ thống tự chuyển đơn sang loại '{fallback.Code}' (nghỉ không lương).";
                }
            }
        }

        var resolvedRoute = ApproverRouteResolver.Resolve(request.ApproverRoute, _currentUser.RoleId);

        // Chứng từ y tế nay upload theo STREAM (nhánh dev) → object key trong MinIO. Gắn CÙNG một
        // object cho mọi bản ghi sinh ra từ đơn này (đơn tách [D2] cũng vẫn là một lần nghỉ, một
        // giấy tờ). Nếu ghi DB hỏng thì xoá object đã upload để không để rác.
        string? medicalCertObjectKey = null;
        if (request.MedicalCertificateStream is not null)
        {
            medicalCertObjectKey = await _certificateUploader.UploadAsync(
                employeeId,
                request.MedicalCertificateStream,
                request.MedicalCertificateContentType ?? string.Empty,
                request.MedicalCertificateSize ?? 0L,
                cancellationToken);
        }

        LeaveRequestEntity NewRequest(LeaveTypeEntity type, DateOnly from, DateOnly to, decimal days) =>
            new()
            {
                EmployeeInformationId = employeeId,
                LeaveTypeId = type.Id,
                StartDate = from,
                EndDate = to,
                TotalDays = days,
                IsPaid = type.IsPaid,
                ApproverRoute = resolvedRoute,
                Status = LeaveRequestStatus.Pending,
                Reason = request.Reason,
                MedicalCertificateObjectKey = medicalCertObjectKey,
                InsuranceCertNumber = request.InsuranceCertNumber?.Trim(),
                IsRetroactive = isRetroactive,
                CreatedAt = _clock.UtcNow,
                CreatedByUserId = userId
            };

        try
        {
            // ── Trường hợp thường: 1 đơn duy nhất ───────────────────────────────────────────
            if (paidSplitDays <= 0 || unpaidType is null)
            {
                var entity = NewRequest(finalType, request.StartDate, request.EndDate, totalDays);
                var id = await _repository.AddAsync(entity, cancellationToken);
                return new CreateLeaveRequestResult(id, finalType.Id, finalType.Code, switched, switchMessage);
            }

            // ── `[D2]`: tách phần CÓ LƯƠNG (dùng nốt quỹ) và phần KHÔNG LƯƠNG ───────────────
            //
            // Cắt theo NGÀY ĐÃ PHÂN CA, không theo ngày lịch: những ngày không phân ca nằm xen
            // giữa (nghỉ tuần, lễ) không tính vào quỹ nên cũng không được tính vào điểm cắt.
            //
            // Phần có lương ôm trọn từ đầu đơn tới ngày phân ca thứ `paidSplitDays`; phần không
            // lương bắt đầu từ ngày phân ca kế tiếp và kéo tới hết đơn. Nhờ vậy `DayTypeResolver`
            // phân loại từng ngày đúng theo cờ `IsPaid` của đơn phủ ngày đó, và cổng bảo hiểm
            // `[D23]` đếm được chính xác số ngày nghỉ không lương rơi vào mỗi kỳ lương.
            var lastPaidDay = assignedDays[paidSplitDays - 1];
            var firstUnpaidDay = assignedDays[paidSplitDays];
            var unpaidDays = totalDays - paidSplitDays;

            var paidPart = NewRequest(leaveType, request.StartDate, lastPaidDay, paidSplitDays);
            var unpaidPart = NewRequest(unpaidType, firstUnpaidDay, request.EndDate, unpaidDays);

            // Ghi cùng một lượt: không được phép còn lại một nửa (xem AddRangeAsync).
            var ids = await _repository.AddRangeAsync(new[] { paidPart, unpaidPart }, cancellationToken);

            return new CreateLeaveRequestResult(
                ids[0], leaveType.Id, leaveType.Code, switched, switchMessage,
                UnpaidId: ids[1], PaidDays: paidSplitDays, UnpaidDays: unpaidDays);
        }
        catch
        {
            await _certificateUploader.TryDeleteAsync(medicalCertObjectKey, CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Các ngày trong [start, end] mà nhân viên đã có <see cref="Domain.Entities.Attendance.EmployeeShiftAssignment"/>,
    /// sắp xếp TĂNG DẦN.
    ///
    /// <para>Bảo đảm Approve có thể sinh <c>AttendanceRecord OnLeave</c> cho từng ngày (mỗi bản ghi
    /// bắt buộc gắn ShiftTemplateId theo constraint <c>CK_AttendanceRecords_ShiftOrOvertime</c>).
    /// Đồng nhất với LateEarly/WFH.</para>
    ///
    /// <para>Trả về DANH SÁCH NGÀY chứ không chỉ số đếm, vì `[D2]` cần biết chính xác ngày nào
    /// thuộc phần có lương để cắt đơn (xem <see cref="TrySplitOverQuota"/>).</para>
    /// </summary>
    private async Task<IReadOnlyList<DateOnly>> GetShiftAssignedDaysAsync(
        int employeeInformationId,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        var assignments = await _shiftAssignmentRepository.GetByEmployeesAndDateRangeAsync(
            new[] { employeeInformationId }, start, end, includeInactiveTemplates: true, cancellationToken);
        return assignments
            .Select(a => a.WorkDate)
            .Distinct()
            .OrderBy(d => d)
            .ToList();
    }
}
