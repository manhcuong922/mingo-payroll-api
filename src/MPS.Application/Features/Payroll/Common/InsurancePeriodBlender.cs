using MPS.Application.Common.Models.Compliance;

namespace MPS.Application.Features.Payroll.Common;

/// <summary>
/// ⭐ [Mục 2] Bộ số bảo hiểm của một kỳ, đã TRỘN THEO TRỌNG SỐ NGÀY khi văn bản đổi giữa kỳ.
/// </summary>
/// <param name="ChangedWithinPeriod">
/// Kỳ có từ 2 phiên bản trở lên (BHXH hoặc lương tối thiểu vùng) ⇒ số dưới đây là số BÌNH QUÂN,
/// không phải số của một văn bản đơn lẻ. Kế toán phải được nói rõ điều này.
/// </param>
public sealed record InsurancePeriodRates(
    decimal BhxhDnRate,
    decimal BhxhNldRate,
    decimal BhytDnRate,
    decimal BhytNldRate,
    decimal BhtnDnRate,
    decimal BhtnNldRate,
    decimal KpcdRate,
    decimal UnionDuesRate,
    decimal UnionDuesCap,
    decimal BhxhBhytCap,
    decimal BhytCap,
    decimal BhtnCap,
    decimal RegionMinWage,
    IReadOnlyList<SocialInsuranceRateSnapshot> SocialVersions,
    IReadOnlyList<MinimumWageSnapshot> MinWageVersions)
{
    public bool ChangedWithinPeriod => SocialVersions.Count > 1 || MinWageVersions.Count > 1;
}

/// <summary>
/// ⭐ [Mục 2] Trộn tỷ lệ và trần bảo hiểm theo TRỌNG SỐ NGÀY LỊCH của kỳ.
///
/// <para><b>Vì sao trộn thay vì tách đôi bảng lương.</b> Khối bảo hiểm trên bảng lương là
/// <c>MIN(nền, trần) × tỷ_lệ</c> với MỘT tỷ lệ và MỘT trần cho mỗi cột. Tách đôi thật sẽ phải nhân
/// đôi 7 cột bảo hiểm và migrate mọi bảng công thức kế toán đã tự chỉnh cùng mọi kỳ lịch sử. Trộn
/// theo trọng số ngày đổ vào đúng field cũ nên bảng công thức, DB, UI và Excel không đổi một dòng.</para>
///
/// <para><b>Sai số đã biết — cố ý chấp nhận.</b> Vì <c>MIN</c> làm hàm không tuyến tính, trộn tỷ lệ
/// và trần riêng rẽ chỉ CHÍNH XÁC TUYỆT ĐỐI với người có nền đóng DƯỚI trần (đại đa số). Người đã
/// CHẠM TRẦN sẽ lệch chút so với tách đôi thật. Kỳ có trộn luôn kèm ghi chú nói rõ điều này.</para>
///
/// <para><b>Trọng số là NGÀY LỊCH của kỳ</b>, không phải ngày công: bảo hiểm là nghĩa vụ theo tháng,
/// "nửa tháng đầu mức cũ, nửa tháng sau mức mới" là cách kế toán và cơ quan BHXH cùng hiểu.</para>
///
/// <para>Thuần (không I/O) để test độc lập.</para>
/// </summary>
public static class InsurancePeriodBlender
{
    /// <summary>Cặp văn bản có hiệu lực tại một ngày cụ thể trong kỳ.</summary>
    public readonly record struct DayRates(SocialInsuranceRateSnapshot Social, MinimumWageSnapshot MinWage);

    public static InsurancePeriodRates Blend(IReadOnlyList<DayRates> perDay)
    {
        if (perDay.Count == 0)
            throw new ArgumentException("Kỳ lương phải có ít nhất một ngày.", nameof(perDay));

        decimal Avg(Func<DayRates, decimal> selector) => perDay.Sum(selector) / perDay.Count;

        return new InsurancePeriodRates(
            BhxhDnRate: Avg(d => d.Social.Funds.Sum(f => f.EmployerPercent)) / 100m,
            BhxhNldRate: Avg(d => d.Social.Funds.Sum(f => f.EmployeePercent)) / 100m,
            BhytDnRate: Avg(d => d.Social.EmployerHealthPercent) / 100m,
            BhytNldRate: Avg(d => d.Social.EmployeeHealthPercent) / 100m,
            BhtnDnRate: Avg(d => d.Social.EmployerUnemploymentPercent) / 100m,
            BhtnNldRate: Avg(d => d.Social.EmployeeUnemploymentPercent) / 100m,
            KpcdRate: Avg(d => d.Social.EmployerUnionFundPercent) / 100m,
            UnionDuesRate: Avg(d => d.Social.EmployeeUnionDuesPercent) / 100m,

            // Trần đoàn phí chặn trên SỐ TIỀN (10% mức lương cơ sở), nên trộn TRÊN TÍCH giống các
            // trần khối BH — mức tham chiếu và tỷ lệ trần có thể đổi cùng lúc.
            UnionDuesCap: Avg(d => d.Social.ReferenceAmount * d.Social.EmployeeUnionDuesCapPercent / 100m),

            // Trần = mức tham chiếu × hệ số, TRỘN TRÊN TÍCH chứ không trộn hai thừa số rồi mới nhân:
            // mức tham chiếu và hệ số có thể đổi cùng lúc, nhân hai số bình quân sẽ ra một con số
            // không ứng với ngày nào cả.
            BhxhBhytCap: Avg(d => d.Social.ReferenceAmount * d.Social.SocialCapMultiplier),
            BhytCap: Avg(d => d.Social.ReferenceAmount * d.Social.HealthCapMultiplier),
            BhtnCap: Avg(d => d.MinWage.GetAppliedAmount() * d.Social.UnemploymentCapMultiplier),

            RegionMinWage: Avg(d => d.MinWage.GetAppliedAmount()),

            SocialVersions: DistinctVersions(perDay.Select(d => d.Social), v => v.VersionId, v => v.ValidFrom),
            MinWageVersions: DistinctVersions(perDay.Select(d => d.MinWage), v => v.VersionId, v => v.ValidFrom));
    }

    private static IReadOnlyList<T> DistinctVersions<T>(
        IEnumerable<T> source, Func<T, int> versionId, Func<T, DateOnly> validFrom) =>
        source.GroupBy(versionId).Select(g => g.First()).OrderBy(validFrom).ToList();
}
