using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Services;
using MPS.Application.Features.Attendance.Attendance.Common;
using MPS.Application.Features.Attendance.Common;
using MPS.Application.Features.Attendance.LateEarlyRequest.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Attendance.Attendance.Commands;

public class UpdateAttendanceByIdCommandHandler : IRequestHandler<UpdateAttendanceByIdCommand, bool>
{
    private readonly IAttendanceRecordRepository _repository;
    private readonly IAttendanceSettingRepository _settingRepository;
    private readonly IAttendanceStatusResolver _statusResolver;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkHourSplitter _splitter;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly IAttendanceEditLockChecker _lockChecker;
    private readonly LateEarlyExcuseResolver _excuseResolver;
    private readonly IPayrollSourceChangeHandler _payrollChange;
    private readonly IClock _clock;

    public UpdateAttendanceByIdCommandHandler(
        IAttendanceRecordRepository repository,
        IAttendanceSettingRepository settingRepository,
        IAttendanceStatusResolver statusResolver,
        ICurrentUserService currentUser,
        IWorkHourSplitter splitter,
        IComplianceLookupService complianceLookup,
        IAttendanceEditLockChecker lockChecker,
        LateEarlyExcuseResolver excuseResolver,
        IPayrollSourceChangeHandler payrollChange,
        IClock clock)
    {
        _repository = repository;
        _settingRepository = settingRepository;
        _statusResolver = statusResolver;
        _currentUser = currentUser;
        _splitter = splitter;
        _complianceLookup = complianceLookup;
        _lockChecker = lockChecker;
        _excuseResolver = excuseResolver;
        _payrollChange = payrollChange;
        _clock = clock;
    }

    public async Task<bool> Handle(UpdateAttendanceByIdCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        await _lockChecker.EnsureCanModifyAsync(
            existing.WorkDate, request.OverrideReason, cancellationToken);

        var checkInAt = request.CheckInAt;
        var checkOutAt = request.CheckOutAt;
        if (!AttendanceTimeRules.HasValidObservedRange(checkInAt, checkOutAt))
            throw new DomainValidationException("CheckOutAt must be later than CheckInAt.");

        var laborLaw = await _complianceLookup.GetLaborLawConstantsAtAsync(existing.WorkDate, cancellationToken);

        // Resolve status: nếu HR truyền cụ thể → giữ nguyên; nếu null và có shift → resolver suy từ giờ chấm.
        // Nếu null và không có shift (OT) → giữ status hiện tại.
        AttendanceStatus finalStatus;
        if (request.Status.HasValue)
        {
            finalStatus = request.Status.Value;
        }
        else if (existing.ShiftTemplate is not null)
        {
            var setting = await _settingRepository.GetOrCreateDefaultAsync(_clock.UtcNow, cancellationToken);
            finalStatus = _statusResolver.Resolve(
                checkInAt, checkOutAt, existing.ShiftTemplate, existing.WorkDate, setting);
        }
        else
        {
            finalStatus = existing.Status;
        }

        // Shift record → dùng AttendanceHourCalculator (cap về khung ca + break) và cộng trả
        // phần đã được miễn bởi đơn Late/Early Approved. OT record → split trực tiếp, không cap.
        decimal dayHours;
        decimal nightHours;
        if (existing.ShiftTemplate is not null)
        {
            var excused = await _excuseResolver.GetExcusedMinutesAsync(
                existing.EmployeeInformationId, existing.WorkDate, cancellationToken);
            (dayHours, nightHours) = AttendanceHourCalculator.Compute(
                _splitter, finalStatus, checkInAt, checkOutAt,
                laborLaw.NightStartTime, laborLaw.NightEndTime,
                existing.ShiftTemplate, existing.WorkDate,
                excusedLateMinutes: excused.LateMinutes,
                excusedEarlyMinutes: excused.EarlyMinutes);
        }
        else
        {
            (dayHours, nightHours) = SplitHoursWithoutShift(
                finalStatus, checkInAt, checkOutAt,
                laborLaw.NightStartTime, laborLaw.NightEndTime);
        }

        existing.CheckInAt = checkInAt;
        existing.CheckOutAt = checkOutAt;
        existing.Status = finalStatus;
        existing.Notes = request.Notes;
        existing.OverrideReason = !string.IsNullOrWhiteSpace(request.OverrideReason)
            ? request.OverrideReason
            : existing.OverrideReason;
        existing.DayHours = dayHours;
        existing.NightHours = nightHours;
        existing.UpdatedAt = _clock.UtcNow;
        existing.UpdatedByUserId = userId;

        var updated = await _repository.UpdateAsync(existing, cancellationToken);

        // BS-6 + BS-5: sửa chấm công (kể cả bản ghi ca OT — nhánh SplitHoursWithoutShift ở dưới) đổi
        // số liệu nguồn của kỳ → kỳ Draft mark stale, kỳ Finalized capture chênh lệch.
        if (updated)
            await _payrollChange.OnSourceChangedAsync(
                existing.WorkDate.Year, existing.WorkDate.Month, existing.EmployeeInformationId,
                $"Sửa chấm công ngày {existing.WorkDate:dd/MM/yyyy}", cancellationToken);

        return updated;
    }

    /// <summary>Chỉ dùng cho OT record (không có shift → không có break, không cap khung ca).</summary>
    private (decimal DayHours, decimal NightHours) SplitHoursWithoutShift(
        AttendanceStatus status,
        DateTime? checkInAt,
        DateTime? checkOutAt,
        TimeOnly nightStart,
        TimeOnly nightEnd)
    {
        if (status is AttendanceStatus.OnLeave or AttendanceStatus.Absent) return (0m, 0m);
        if (checkInAt is null || checkOutAt is null) return (0m, 0m);

        var breakdown = _splitter.Split(new WorkHourDateTimeSplitInput(
            CheckInAt: checkInAt.Value,
            CheckOutAt: checkOutAt.Value,
            BreakStart: null,
            BreakEnd: null,
            NightStart: nightStart,
            NightEnd: nightEnd));
        return (breakdown.DayHours, breakdown.NightHours);
    }
}
