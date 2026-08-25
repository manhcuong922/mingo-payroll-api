using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Payroll.Common;

/// <summary>
/// BS-2 (phần A) — phụ trội cho giờ công THƯỜNG (ca chính thức, không qua đơn OT).
///
/// Lương giờ thường được trả qua cột THU NHẬP = mức lương phân bổ theo NGÀY CÔNG, tức mọi ngày đều
/// được trả **100%** như nhau. Nhưng vẫn có ca chính thức rơi vào ngày nghỉ tuần / ngày lễ — luật
/// đòi 200% / 300% (Đ.98.1) ⇒ phải trả thêm.
///
/// <para>⭐ HOLIDAY-400-2026-08 — <b>trả thêm bao nhiêu thì tuỳ khoản 100% kia là tiền GÌ</b>, và
/// hai loại ngày khác nhau ở đúng chỗ này:</para>
/// <list type="bullet">
///   <item><b>Ngày nghỉ hằng tuần</b> — không đi làm thì KHÔNG có đồng nào (T7/CN không nằm trong
///     công chuẩn, cũng không có khoản bù nào). Nên 100% đã trả chính là tiền công của việc đi làm
///     hôm đó ⇒ chỉ cần bù <b>phần chênh</b> tới 200%: <c>200% − 100% = 100%</c>. Tổng 200%, đúng
///     Đ.98.1b.</item>
///   <item><b>Ngày lễ / ngày nghỉ có hưởng lương</b> — Đ.112.1 cho nghỉ mà <b>vẫn hưởng nguyên
///     lương</b>, và engine đã thực hiện đúng thế: ở nhà vẫn được 1 công (khoá <c>CONGLE</c>). Vậy
///     100% đó là tiền của QUYỀN NGHỈ LỄ, KHÔNG phải tiền công của việc đi làm — không được đem trừ
///     vào mức 300% mà Đ.98.1c đòi cho giờ làm. Phải trả <b>nguyên 300%</b> lên trên ⇒ tổng
///     <b>400%</b>.</item>
/// </list>
///
/// <para>Trước bản này cả hai loại ngày đều lấy "phần chênh", nên đi làm ngày lễ chỉ được
/// <c>100% + 200% = 300%</c>: phần thưởng cho việc hy sinh ngày nghỉ lễ chỉ còn <b>+200%</b> thay
/// vì +300%, tức thiếu đúng một ngày lương mỗi ngày lễ đi làm.</para>
///
/// Hai khoản ĐỘC LẬP nhau (chốt PO 2026-07-21) vì là hai yếu tố khác nhau — loại NGÀY và khung GIỜ:
/// <list type="bullet">
///   <item><b>Phụ trội loại ngày</b> = giờ × đơn giá × hệ_số_phụ_trội (xem <c>PremiumRate</c>)</item>
///   <item><b>Phụ cấp làm đêm</b> (Đ.98.2) = giờ đêm × đơn giá × NightWorkSurchargePercent</item>
/// </list>
/// VD 8h đêm ngày lễ: phụ trội lễ (+300%) + phụ cấp đêm (+30%) = tổng 430% cho ngày đó.
///
/// Cả hai là phần trả CAO HƠN mức ngày thường ⇒ **miễn thuế TNCN** (Đ.3 TT111/2013) — người gọi trừ
/// khỏi thu nhập chịu thuế, giống cột "Tăng ca miễn thuế" (<c>CL_GLT</c>) sẵn có.
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

            var extraRate = PremiumRate(seg.DayType, labor, normalRate) / 100m;
            if (extraRate > 0m) dayTypePremium += hours * hourlyRate * extraRate;

            nightPremium += seg.NightHours * hourlyRate * nightSurcharge;
        }

        return new Result(
            Math.Round(dayTypePremium, 0, MidpointRounding.AwayFromZero),
            Math.Round(nightPremium, 0, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// ⭐ HOLIDAY-400-2026-08 — phần trăm phải trả THÊM lên trên 100% mà cột Thu nhập đã trả.
    ///
    /// <list type="bullet">
    ///   <item><b>Nghỉ hằng tuần</b>: 100% kia là tiền công của chính việc đi làm hôm đó ⇒ chỉ bù
    ///     phần chênh <c>200% − 100%</c>. Tổng 200% (Đ.98.1b).</item>
    ///   <item><b>Lễ / nghỉ có hưởng lương</b>: 100% kia là tiền của quyền nghỉ lễ (Đ.112.1, engine
    ///     trả qua <c>CONGLE</c> kể cả khi ở nhà) ⇒ trả NGUYÊN 300% cho giờ làm. Tổng 400%
    ///     (Đ.98.1c — "chưa kể tiền lương ngày nghỉ lễ").</item>
    ///   <item><b>Ngày thường</b>: 0.</item>
    /// </list>
    /// </summary>
    private static decimal PremiumRate(
        DayType dayType, LaborLawConstantsSnapshot labor, decimal normalRate) => dayType switch
    {
        DayType.WeeklyRestDay => labor.WorkRateWeeklyRestFloor - normalRate,
        DayType.PublicHoliday => labor.WorkRateHolidayFloor,
        DayType.PaidLeaveDay => labor.WorkRateHolidayFloor,
        _ => 0m,
    };
}
