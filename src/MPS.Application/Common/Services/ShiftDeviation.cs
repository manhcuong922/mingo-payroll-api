using MPS.Domain.Entities.Attendance;

namespace MPS.Application.Common.Services;

/// <summary>
/// Số phút NLĐ vào muộn / về sớm so với ca được phân của <b>một</b> bản ghi chấm công.
///
/// <para>Tách riêng vì hai nơi cùng cần đúng phép đo này và trước đây mỗi nơi tự tính:
/// <see cref="AttendanceStatusResolver"/> (suy Present/Late/EarlyLeave) và
/// <see cref="WorkDayWeightCalculator"/> (miễn phần lệch nằm trong grace khi tính công). Lệch nhau
/// một dòng là hệ thống nói "đúng giờ" nhưng bảng lương lại trừ tiền.</para>
/// </summary>
public readonly record struct ShiftDeviation(int LateMinutes, int EarlyLeaveMinutes)
{
    public static readonly ShiftDeviation None = new(0, 0);

    /// <summary>
    /// Đo lệch giờ. Không có check-in ⇒ <see cref="None"/> (không có gì để so). Không có check-out
    /// ⇒ chỉ đo phần vào muộn — quên chấm ra KHÔNG bị quy thành "về sớm cả ca".
    /// </summary>
    public static ShiftDeviation Measure(
        DateTime? checkInAt,
        DateTime? checkOutAt,
        ShiftTemplate shift,
        DateOnly workDate)
    {
        ArgumentNullException.ThrowIfNull(shift);

        if (checkInAt is null) return None;

        var plannedStart = workDate.ToDateTime(shift.StartTime, DateTimeKind.Unspecified);
        // EndTime <= StartTime ⇒ ca vắt qua nửa đêm, giờ tan ca thuộc ngày hôm sau.
        var plannedEnd = shift.EndTime > shift.StartTime
            ? workDate.ToDateTime(shift.EndTime, DateTimeKind.Unspecified)
            : workDate.AddDays(1).ToDateTime(shift.EndTime, DateTimeKind.Unspecified);

        var lateMinutes = (int)Math.Max(0, (checkInAt.Value - plannedStart).TotalMinutes);
        var earlyMinutes = checkOutAt.HasValue
            ? (int)Math.Max(0, (plannedEnd - checkOutAt.Value).TotalMinutes)
            : 0;

        return new ShiftDeviation(lateMinutes, earlyMinutes);
    }
}
