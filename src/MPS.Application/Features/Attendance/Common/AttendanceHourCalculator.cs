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
///   - <paramref name="excusedLateMinutes"/> / <paramref name="excusedEarlyMinutes"/>
///     là tổng số phút đi trễ / về sớm đã được Approved bởi đơn Late/Early cho ngày đó.
///     Cửa sổ hiệu dụng được DỜI về sớm/muộn tương ứng (cap tại khung ca) trước khi
///     splitter chia giờ ⇒ NV được cộng trả giờ công đã mất do trễ/sớm được miễn.
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
        DateOnly workDate,
        int excusedLateMinutes = 0,
        int excusedEarlyMinutes = 0)
    {
        if (status is AttendanceStatus.OnLeave or AttendanceStatus.Absent)
            return (0m, 0m);

        if (checkInAt is null || checkOutAt is null)
            return (0m, 0m);

        var (effectiveIn, effectiveOut) = ClampToShiftWindow(
            checkInAt.Value, checkOutAt.Value,
            shift.StartTime, shift.EndTime, workDate);

        (effectiveIn, effectiveOut) = ApplyExcusedMinutes(
            effectiveIn, effectiveOut,
            shift.StartTime, shift.EndTime, workDate,
            excusedLateMinutes, excusedEarlyMinutes);

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
    /// Dời cửa sổ hiệu dụng để bù số phút đi trễ / về sớm đã được duyệt:
    ///   - excusedLateMinutes  → coi như check-in sớm hơn tương ứng (không vượt shiftStart).
    ///   - excusedEarlyMinutes → coi như check-out muộn hơn tương ứng (không vượt shiftEnd).
    /// Không có tác dụng khi bản ghi chưa từng bị trễ / sớm (cửa sổ đã sát khung ca).
    /// </summary>
    private static (DateTime EffectiveIn, DateTime EffectiveOut) ApplyExcusedMinutes(
        DateTime effectiveIn,
        DateTime effectiveOut,
        TimeOnly shiftStart,
        TimeOnly shiftEnd,
        DateOnly workDate,
        int excusedLateMinutes,
        int excusedEarlyMinutes)
    {
        if (excusedLateMinutes <= 0 && excusedEarlyMinutes <= 0)
            return (effectiveIn, effectiveOut);

        var shiftStartDt = workDate.ToDateTime(shiftStart, DateTimeKind.Unspecified);
        var shiftEndDt = shiftEnd > shiftStart
            ? workDate.ToDateTime(shiftEnd, DateTimeKind.Unspecified)
            : workDate.AddDays(1).ToDateTime(shiftEnd, DateTimeKind.Unspecified);

        if (excusedLateMinutes > 0)
        {
            var shifted = effectiveIn.AddMinutes(-excusedLateMinutes);
            if (shifted < shiftStartDt) shifted = shiftStartDt;
            effectiveIn = shifted;
        }

        if (excusedEarlyMinutes > 0)
        {
            var shifted = effectiveOut.AddMinutes(excusedEarlyMinutes);
            if (shifted > shiftEndDt) shifted = shiftEndDt;
            effectiveOut = shifted;
        }

        return (effectiveIn, effectiveOut);
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
