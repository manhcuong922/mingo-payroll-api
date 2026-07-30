using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Common;

/// <summary>
/// Case 3 + 5 — dựng danh sách <see cref="OvertimePayCalculator.OvertimeSegment"/> từ khung giờ OT thô,
/// TÁCH THEO NGÀY LỊCH khi OT vắt qua nửa đêm để mỗi ngày áp đúng loại-ngày (Đ.98):
///
///  • Case 5: OT 22:00→02:00 ngày D (ngày thường) mà D+1 là NGÀY LỄ ⇒ phần 22:00–24:00 tính hệ số đêm
///    ngày thường, phần 00:00–02:00 tính hệ số đêm NGÀY LỄ. Trước đây cả khối dùng một loại-ngày của D.
///
///  • Case 3: phần giờ ĐÊM đứng SAU giờ NGÀY (khối bắt đầu trong giờ ban ngày rồi vắt vào đêm, vd
///    20:00→24:00) thì đơn giá gốc cộng 20% là 150% (Đ.98.3) ⇒ đêm ngày thường = 210% thay vì 200%.
///    Khối bắt đầu trong giờ đêm (22:00→02:00, hoặc phần sau nửa đêm) KHÔNG có giờ ngày đứng trước.
///
/// Thuần (không I/O) để test độc lập. Caller (PayrollDataCollector) resolve loại-ngày cho D và D+1.
/// </summary>
public static class OvertimeSegmentBuilder
{
    private const int MinutesPerDay = 24 * 60;

    public static IReadOnlyList<OvertimePayCalculator.OvertimeSegment> Build(
        TimeOnly start,
        TimeOnly end,
        DayType dayTypeD,
        DayType dayTypeNextDay,
        TimeOnly nightStart,
        TimeOnly nightEnd)
    {
        var segments = new List<OvertimePayCalculator.OvertimeSegment>(2);
        var startMin = ToMinutes(start);
        var endMin = ToMinutes(end);
        var nightStartMin = ToMinutes(nightStart);
        var nightEndMin = ToMinutes(nightEnd);

        if (endMin > startMin)
        {
            // Không vắt nửa đêm — trọn trong ngày D.
            AddSegment(segments, startMin, endMin, dayTypeD, nightStartMin, nightEndMin);
        }
        else
        {
            // Vắt nửa đêm: [start, 24:00) trên D + [00:00, end) trên D+1.
            AddSegment(segments, startMin, MinutesPerDay, dayTypeD, nightStartMin, nightEndMin);
            AddSegment(segments, 0, endMin, dayTypeNextDay, nightStartMin, nightEndMin);
        }

        return segments;
    }

    private static void AddSegment(
        List<OvertimePayCalculator.OvertimeSegment> segments,
        int startMin, int endMin, DayType dayType, int nightStartMin, int nightEndMin)
    {
        if (endMin <= startMin) return;

        var (dayHours, nightHours) = SplitDayNight(startMin, endMin, nightStartMin, nightEndMin);
        if (dayHours + nightHours <= 0m) return;

        // Đêm đứng sau ngày OT: khối bắt đầu trong giờ BAN NGÀY (không thuộc khung đêm) và có giờ đêm.
        var hasPrevDayOt = nightHours > 0m && !IsNight(startMin, nightStartMin, nightEndMin);
        segments.Add(new OvertimePayCalculator.OvertimeSegment(dayType, dayHours, nightHours, hasPrevDayOt));
    }

    private static (decimal DayHours, decimal NightHours) SplitDayNight(
        int startMin, int endMin, int nightStartMin, int nightEndMin)
    {
        int nightMin = nightStartMin > nightEndMin
            // Khung đêm vắt nửa đêm: [nightStart, 24:00) ∪ [00:00, nightEnd).
            ? Overlap(startMin, endMin, nightStartMin, MinutesPerDay) + Overlap(startMin, endMin, 0, nightEndMin)
            // Khung đêm trong ngày: [nightStart, nightEnd).
            : Overlap(startMin, endMin, nightStartMin, nightEndMin);

        var dayMin = (endMin - startMin) - nightMin;
        return (Round(dayMin), Round(nightMin));
    }

    private static bool IsNight(int minute, int nightStartMin, int nightEndMin) =>
        nightStartMin > nightEndMin
            ? minute >= nightStartMin || minute < nightEndMin
            : minute >= nightStartMin && minute < nightEndMin;

    private static int Overlap(int aStart, int aEnd, int bStart, int bEnd) =>
        Math.Max(0, Math.Min(aEnd, bEnd) - Math.Max(aStart, bStart));

    private static decimal Round(int minutes) => Math.Round((decimal)minutes / 60m, 2);

    private static int ToMinutes(TimeOnly t) => t.Hour * 60 + t.Minute;
}
