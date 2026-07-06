using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Attendance;
using ShiftTemplateEntity = MPS.Domain.Entities.Attendance.ShiftTemplate;

namespace MPS.Application.Features.Attendance.Common;

/// <summary>
/// Helper gọi splitter cho 1 attendance record. Quy ước:
///   - Status = OnLeave / Absent / NotChecked  → (0, 0).
///   - Thiếu CheckIn hoặc CheckOut             → (0, 0).
///   - Có cả CheckIn/CheckOut → splitter compute với break từ shift template.
///
/// Đặt ở Application layer (không phải Infrastructure) để command handler gọi
/// trực tiếp mà không cần inject splitter+constants riêng.
/// </summary>
public static class AttendanceHourCalculator
{
    public static (decimal DayHours, decimal NightHours) Compute(
        IWorkHourSplitter splitter,
        AttendanceStatus status,
        DateTime? checkInAt,
        DateTime? checkOutAt,
        TimeOnly nightStart,
        TimeOnly nightEnd,
        ShiftTemplateEntity shift)
    {
        if (status is AttendanceStatus.OnLeave or AttendanceStatus.Absent)
            return (0m, 0m);

        if (checkInAt is null || checkOutAt is null)
            return (0m, 0m);

        var breakdown = splitter.Split(new WorkHourDateTimeSplitInput(
            CheckInAt: checkInAt.Value,
            CheckOutAt: checkOutAt.Value,
            BreakStart: shift.BreakStartTime,
            BreakEnd: shift.BreakEndTime,
            NightStart: nightStart,
            NightEnd: nightEnd));

        return (breakdown.DayHours, breakdown.NightHours);
    }

    public static (decimal DayHours, decimal NightHours) Compute(
        IWorkHourSplitter splitter,
        AttendanceStatus status,
        TimeOnly? checkIn,
        TimeOnly? checkOut,
        TimeOnly nightStart,
        TimeOnly nightEnd,
        ShiftTemplateEntity shift)
    {
        if (status is AttendanceStatus.OnLeave or AttendanceStatus.Absent)
            return (0m, 0m);

        if (checkIn is null || checkOut is null)
            return (0m, 0m);

        var breakdown = splitter.Split(new WorkHourSplitInput(
            CheckIn: checkIn.Value,
            CheckOut: checkOut.Value,
            BreakStart: shift.BreakStartTime,
            BreakEnd: shift.BreakEndTime,
            NightStart: nightStart,
            NightEnd: nightEnd));

        return (breakdown.DayHours, breakdown.NightHours);
    }
}
