using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Attendance.Common;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Commands;

public class UpdateAttendanceByIdCommandHandler : IRequestHandler<UpdateAttendanceByIdCommand, bool>
{
    private readonly IAttendanceRecordRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkHourSplitter _splitter;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly IAttendanceEditLockChecker _lockChecker;
    private readonly IClock _clock;

    public UpdateAttendanceByIdCommandHandler(
        IAttendanceRecordRepository repository,
        ICurrentUserService currentUser,
        IWorkHourSplitter splitter,
        IComplianceLookupService complianceLookup,
        IAttendanceEditLockChecker lockChecker,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _splitter = splitter;
        _complianceLookup = complianceLookup;
        _lockChecker = lockChecker;
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

        // Shift record → dùng AttendanceHourCalculator (cap về khung ca + break).
        // OT record (không có ShiftTemplate) → split trực tiếp, không cap.
        var (dayHours, nightHours) = existing.ShiftTemplate is not null
            ? AttendanceHourCalculator.Compute(
                _splitter, request.Status, checkInAt, checkOutAt,
                laborLaw.NightStartTime, laborLaw.NightEndTime,
                existing.ShiftTemplate, existing.WorkDate)
            : SplitHoursWithoutShift(
                request.Status, checkInAt, checkOutAt,
                laborLaw.NightStartTime, laborLaw.NightEndTime);

        existing.CheckInAt = checkInAt;
        existing.CheckOutAt = checkOutAt;
        existing.Status = request.Status;
        existing.Notes = request.Notes;
        existing.OverrideReason = !string.IsNullOrWhiteSpace(request.OverrideReason)
            ? request.OverrideReason
            : existing.OverrideReason;
        existing.DayHours = dayHours;
        existing.NightHours = nightHours;
        existing.UpdatedAt = _clock.UtcNow;
        existing.UpdatedByUserId = userId;

        return await _repository.UpdateAsync(existing, cancellationToken);
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
