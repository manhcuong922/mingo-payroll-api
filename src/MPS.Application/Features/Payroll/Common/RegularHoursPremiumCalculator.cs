using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Payroll.Common;

/// <summary>
/// BS-2 (phần A) — phụ trội cho giờ công THƯỜNG (ca chính thức, không qua đơn OT).
///
/// Lương giờ thường được trả qua cột THU NHẬP = mức lương phân bổ theo NGÀY CÔNG, tức mọi ngày đều
/// được trả **100%** như nhau. Nhưng xưởng chạy ca xoay nên vẫn có ca chính thức rơi vào ngày nghỉ
/// tuần / ngày lễ — luật đòi 200% / 300% (Đ.98.1) ⇒ phải trả **phần chênh** còn thiếu.
///
/// Hai khoản ĐỘC LẬP nhau (chốt PO 2026-07-21) vì là hai yếu tố khác nhau — loại NGÀY và khung GIỜ:
/// <list type="bullet">
///   <item><b>Phụ trội loại ngày</b> = giờ × đơn giá × (hệ_số_loại_ngày − hệ_số_thường)</item>
///   <item><b>Phụ cấp làm đêm</b> (Đ.98.2) = giờ đêm × đơn giá × NightWorkSurchargePercent</item>
/// </list>
/// VD 8h đêm ngày lễ: phụ trội lễ (+200%) + phụ cấp đêm (+30%) = tổng 330% cho ngày đó.
///
/// Cả hai là phần trả CAO HƠN mức ngày thường ⇒ **miễn thuế TNCN** (Đ.3 TT111/2013) — người gọi trừ
/// khỏi thu nhập chịu thuế, giống cột "Chênh lệch làm thêm giờ" sẵn có.
///
/// Thuần số học (không DbContext) để test độc lập.
/// </summary>
public static class RegularHoursPremiumCalculator
{
    /// <summary>
    /// Giờ công thường của 1 ngày, kèm loại ngày đã resolve.
    /// ⭐ [Mục 1] <paramref name="Date"/> quyết định lấy hằng số luật của ngày nào.
    /// </summary>
    public readonly record struct WorkDaySegment(
        DateOnly Date, DayType DayType, decimal DayHours, decimal NightHours);

    /// <param name="DayTypePremium">Phụ trội do ngày nghỉ tuần / lễ — 0 nếu chỉ làm ngày thường.</param>
    /// <param name="NightPremium">Phụ cấp làm đêm 30% cho giờ thường.</param>
    public readonly record struct Result(decimal DayTypePremium, decimal NightPremium);

    public static Result Accumulate(
        IEnumerable<WorkDaySegment> segments,
        decimal hourlyRate,
        LaborLawTimeline timeline)
    {
        if (hourlyRate <= 0m) return new Result(0m, 0m);

        decimal dayTypePremium = 0m, nightPremium = 0m;

        foreach (var seg in segments)
        {
            // ⭐ [Mục 1] Sàn ngày nghỉ/lễ và % phụ cấp đêm tra theo NGÀY CÔNG, không theo ngày cuối kỳ.
            var labor = timeline.For(seg.Date);
            var normalRate = labor.WorkRateNormalFloor;
            var nightSurcharge = labor.NightWorkSurchargePercent / 100m;
            var hours = seg.DayHours + seg.NightHours;

            // Phần CÒN THIẾU so với mức đã trả qua cột THU NHẬP (luôn = hệ số ngày thường).
            var extraRate = (DayTypeRate(seg.DayType, labor) - normalRate) / 100m;
            if (extraRate > 0m) dayTypePremium += hours * hourlyRate * extraRate;

            nightPremium += seg.NightHours * hourlyRate * nightSurcharge;
        }

        return new Result(
            Math.Round(dayTypePremium, 0, MidpointRounding.AwayFromZero),
            Math.Round(nightPremium, 0, MidpointRounding.AwayFromZero));
    }

    // Cùng bảng hệ số Đ.98.1 mà OT dùng — nghỉ tuần 200%, lễ / nghỉ có lương 300%, thường 100%.
    private static decimal DayTypeRate(DayType dayType, LaborLawConstantsSnapshot labor) => dayType switch
    {
        DayType.WeeklyRestDay => labor.WorkRateWeeklyRestFloor,
        DayType.PublicHoliday => labor.WorkRateHolidayFloor,
        DayType.PaidLeaveDay => labor.WorkRateHolidayFloor,
        _ => labor.WorkRateNormalFloor,
    };
}
