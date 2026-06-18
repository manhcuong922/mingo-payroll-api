// DESIGN NOTE — tại sao KHÔNG dùng enum:
// Cơ chế xác định điều kiện đóng BHXH có thể mở rộng (vd thêm theo loại hợp đồng,
// theo ngành nghề...) trong tương lai. Lưu dạng string để tránh redeploy (NFR-M05).

namespace MPS.Domain.Enums.Compliance;

/// <summary>
/// Cơ chế xác định nhân viên đủ điều kiện đóng BHXH/BHYT/BHTN, gắn theo
/// SocialInsuranceRateVersion (effective-dating cùng % đóng).
/// </summary>
public static class SocialInsuranceEligibilityModes
{
    /// <summary>Theo loại hợp đồng (Probation/Official) — SocialInsuranceRateVersion.EligibleContractSubtypesCsv.</summary>
    public const string ByContractSubtype = "ByContractSubtype";

    /// <summary>Theo số tháng thâm niên — SocialInsuranceRateVersion.MinTenureMonths.</summary>
    public const string ByTenureMonths = "ByTenureMonths";

    public static readonly IReadOnlyList<string> All = [ByContractSubtype, ByTenureMonths];
}
