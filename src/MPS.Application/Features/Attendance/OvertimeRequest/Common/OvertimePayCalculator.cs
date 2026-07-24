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
    public readonly record struct OvertimeSegment(DayType DayType, decimal DayHours, decimal NightHours);

    public static (decimal Full, decimal Premium) Accumulate(
        IEnumerable<OvertimeSegment> segments,
        decimal hourlyRate,
        IOvertimeMultiplierCalculator multiplier,
        LaborLawConstantsSnapshot labor)
    {
        if (hourlyRate <= 0m) return (0m, 0m);

        decimal full = 0m, premium = 0m;
        foreach (var seg in segments)
        {
            // Phạm vi BS-2 = sửa hệ số theo LOẠI NGÀY. Giữ hasPrevDayOt=false (như code cũ) để giờ đêm
            // ngày THƯỜNG vẫn ra 2.0 y hệt trước — không đổi số ngoài dự kiến. Tinh chỉnh "đêm sau ngày OT
            // = 20%×150%" (Đ.98.3) là cải tiến riêng, để lần khác nếu cần.
            var dayMult = multiplier.Calculate(seg.DayType, isNight: false, hasPrevDayOt: false, labor) / 100m;
            var nightMult = multiplier.Calculate(seg.DayType, isNight: true, hasPrevDayOt: false, labor) / 100m;

            full += hourlyRate * (seg.DayHours * dayMult + seg.NightHours * nightMult);
            premium += hourlyRate * (seg.DayHours * (dayMult - 1m) + seg.NightHours * (nightMult - 1m));
        }

        return (
            Math.Round(full, 0, MidpointRounding.AwayFromZero),
            Math.Round(premium, 0, MidpointRounding.AwayFromZero));
    }
}
