using MediatR;
using MPS.Application.Common.Exceptions;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Commands;

/// <summary>
/// BR-2 (DC-07): DM may edit only when (today - WorkDate) ≤ 3 days.
/// BR-3: HR with Override permission may edit anytime but OverrideReason is required.
/// Multi-shift: key bao gồm ShiftTemplateId — sửa đúng record của ca cụ thể.
/// </summary>
public class UpdateAttendanceCommandHandler : IRequestHandler<UpdateAttendanceCommand, bool>
{
    private const int EditWindowDays = 3;

    private readonly IAttendanceRecordRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkHourSplitter _splitter;
    private readonly IRegularHoursCapChecker _capChecker;
    private readonly IClock _clock;

    public UpdateAttendanceCommandHandler(
        IAttendanceRecordRepository repository,
        ICurrentUserService currentUser,
        IWorkHourSplitter splitter,
        IRegularHoursCapChecker capChecker,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _splitter = splitter;
        _capChecker = capChecker;
        _clock = clock;
    }

    public async Task<bool> Handle(UpdateAttendanceCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByKeyAsync(
            request.EmployeeInformationId, request.WorkDate, request.ShiftTemplateId, cancellationToken);
        if (existing is null) return false;

        var hasOverride = _currentUser.HasPermission(Permissions.AttendanceAdjustmentOverrideAll);
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var daysSince = today.DayNumber - request.WorkDate.DayNumber;

        if (!hasOverride)
        {
            if (daysSince > EditWindowDays)
                throw new DomainValidationException(
                    $"BR-2: Cannot edit attendance for {request.WorkDate:yyyy-MM-dd} — outside the {EditWindowDays}-day edit window.");
        }
        else
        {
            // BR-3: HR override requires OverrideReason
            if (daysSince > EditWindowDays && string.IsNullOrWhiteSpace(request.OverrideReason))
                throw new DomainValidationException(
                    "BR-3: OverrideReason is required when HR edits an attendance record outside the 3-day edit window.");
        }

        var (dayHours, nightHours) = AttendanceHourCalculator.Compute(
            _splitter, request.Status, request.CheckInTime, request.CheckOutTime, existing.ShiftTemplate);

        // §9 Phase 1b — Cap 48h/tuần warn-then-confirm (Điều 105 BLLĐ).
        // Check TRƯỚC khi mutate existing — tránh EF change tracker làm lệch existing total trong checker query.
        if (!request.AcknowledgeWeeklyOvertimeWarnings)
        {
            var delta = (dayHours + nightHours) - (existing.DayHours + existing.NightHours);
            if (delta != 0)
            {
                var deltas = new[] { new WeeklyCapDelta(request.EmployeeInformationId, request.WorkDate, delta) };
                var dailyWarnings = await _capChecker.CheckDailyAttendanceAsync(
                    deltas,
                    cancellationToken);
                var weeklyWarnings = await _capChecker.CheckAttendanceAsync(
                    deltas,
                    cancellationToken);
                if (dailyWarnings.Count > 0 || weeklyWarnings.Count > 0)
                    throw new RegularHoursCapWarningException(dailyWarnings, weeklyWarnings);
            }
        }

        existing.CheckInTime = request.CheckInTime;
        existing.CheckOutTime = request.CheckOutTime;
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
}
