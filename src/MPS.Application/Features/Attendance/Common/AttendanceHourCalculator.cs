using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Attendance;
using ShiftTemplateEntity = MPS.Domain.Entities.Attendance.ShiftTemplate;

namespace MPS.Application.Features.Attendance.Common;

/// <summary>
/// Helper gọi splitter cho 1 attendance record. Quy ước:
///   - Status = OnLeave / Absent / NotChecked  → (0, 0).
///   - Thiếu CheckIn hoặc CheckOut             → (0, 0).
///   - Có cả CheckIn/CheckOut → CAP về khung ca (StartTime..EndTime của ShiftTemplate)
///     rồi splitter compute với break từ shift template.
///     Chấm vào SỚM trước ShiftStart hoặc chấm ra MUỘN sau ShiftEnd không tính công
///     thường — muốn tính giờ ngoài ca, NV phải đăng ký OvertimeRequest.
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
        ShiftTemplateEntity shift,
        DateOnly workDate)
    {
        if (status is AttendanceStatus.OnLeave or AttendanceStatus.Absent)
            return (0m, 0m);

        if (checkInAt is null || checkOutAt is null)
            return (0m, 0m);

        var (effectiveIn, effectiveOut) = ClampToShiftWindow(
            checkInAt.Value, checkOutAt.Value,
            shift.StartTime, shift.EndTime, workDate);

        if (effectiveOut <= effectiveIn)
            return (0m, 0m);

        var breakdown = splitter.Split(new WorkHourDateTimeSplitInput(
            CheckInAt: effectiveIn,
            CheckOutAt: effectiveOut,
            BreakStart: shift.BreakStartTime,
            BreakEnd: shift.BreakEndTime,
            NightStart: nightStart,
            NightEnd: nightEnd));

        return (breakdown.DayHours, breakdown.NightHours);
    }

    /// <summary>
    /// Cap check-in/check-out về khung giờ ca (workDate + StartTime..EndTime).
    /// Hỗ trợ ca cross-midnight (EndTime ≤ StartTime → EndTime rơi vào ngày sau).
    /// </summary>
    public static (DateTime EffectiveIn, DateTime EffectiveOut) ClampToShiftWindow(
        DateTime checkIn, DateTime checkOut,
        TimeOnly shiftStart, TimeOnly shiftEnd,
        DateOnly workDate)
    {
        var shiftStartDt = workDate.ToDateTime(shiftStart, DateTimeKind.Unspecified);
        var shiftEndDt = shiftEnd > shiftStart
            ? workDate.ToDateTime(shiftEnd, DateTimeKind.Unspecified)
            : workDate.AddDays(1).ToDateTime(shiftEnd, DateTimeKind.Unspecified);

        var effectiveIn = checkIn < shiftStartDt ? shiftStartDt : checkIn;
        var effectiveOut = checkOut > shiftEndDt ? shiftEndDt : checkOut;
        return (effectiveIn, effectiveOut);
    }

}
