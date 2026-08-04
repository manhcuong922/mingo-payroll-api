using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Common;

/// <summary>
/// BS-2 — quy giờ OT (đã tách ngày/đêm + biết loại ngày) ra TIỀN theo hệ số Đ.98:
///   Full    = tổng tiền OT               = hourlyRate × Σ(giờ × hệ_số_đầy_đủ)
///   Premium = phần vượt 100% (miễn thuế) = hourlyRate × Σ(giờ × (hệ_số_đầy_đủ − 1))
/// Hệ số lấy từ <see cref="IOvertimeMultiplierCalculator"/> (đọc LaborLawConstants), không hardcode.
/// Tách riêng khỏi PayrollDataCollector để test được phần số học mà không cần DbContext.
/// </summary>
public static class OvertimePayCalculator
{
    /// <param name="Date">
    /// ⭐ [Mục 1] NGÀY LỊCH của khối giờ này — quyết định lấy hằng số luật nào. OT vắt nửa đêm được
    /// <see cref="OvertimeSegmentBuilder"/> tách thành 2 segment mang 2 ngày khác nhau, nên luật đổi
    /// đúng lúc giao ngày vẫn áp chuẩn cho từng phần giờ.
    /// </param>
    /// <param name="HasPrevDayOt">
    /// Đ.98.3 — giờ OT ĐÊM đứng SAU giờ OT NGÀY (trong cùng ngày, cùng khối) thì đơn giá gốc để cộng
    /// 20% là hệ số OT ngày (150% ngày thường) thay vì 100%. Chỉ ảnh hưởng phần giờ đêm ngày THƯỜNG.
    /// </param>
    public readonly record struct OvertimeSegment(
        DateOnly Date, DayType DayType, decimal DayHours, decimal NightHours, bool HasPrevDayOt = false);

    public static (decimal Full, decimal Premium) Accumulate(
        IEnumerable<OvertimeSegment> segments,
        decimal hourlyRate,
        IOvertimeMultiplierCalculator multiplier,
        LaborLawTimeline timeline)
    {
        if (hourlyRate <= 0m) return (0m, 0m);

        decimal full = 0m, premium = 0m;
        foreach (var seg in segments)
        {
            // ⭐ [Mục 1] Hệ số tra theo NGÀY của segment, không theo ngày cuối kỳ: giờ OT ngày 10/6 ăn
            // hệ số của luật cũ, giờ OT ngày 20/6 ăn hệ số luật mới.
            var labor = timeline.For(seg.Date);

            // hasPrevDayOt chỉ áp cho phần giờ ĐÊM (Đ.98.3); giờ NGÀY không dùng cờ này. Segment đã được
            // OvertimeSegmentBuilder tách theo loại ngày + đặt cờ đúng (đêm sau ngày OT = 20%×150%).
            var dayMult = multiplier.Calculate(seg.DayType, isNight: false, hasPrevDayOt: false, labor) / 100m;
            var nightMult = multiplier.Calculate(seg.DayType, isNight: true, hasPrevDayOt: seg.HasPrevDayOt, labor) / 100m;

            full += hourlyRate * (seg.DayHours * dayMult + seg.NightHours * nightMult);
            premium += hourlyRate * (seg.DayHours * (dayMult - 1m) + seg.NightHours * (nightMult - 1m));
        }

        return (
            Math.Round(full, 0, MidpointRounding.AwayFromZero),
            Math.Round(premium, 0, MidpointRounding.AwayFromZero));
    }
}
