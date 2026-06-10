// [CHANGED] RegionCode từ enum → string để không buộc redeploy khi luật tái phân vùng.
// Validator dùng RegionCodes.All để cross-check giá trị hợp lệ.

namespace MPS.Domain.Entities.Compliance;

public class MinimumWageRegion
{
    public int Id { get; set; }
    public int MinimumWageVersionId { get; set; }
    public MinimumWageVersion MinimumWageVersion { get; set; } = null!;

    /// <summary>
    /// Ký hiệu vùng theo NĐ hiện hành, vd "I", "II", "III", "IV".
    /// Lưu string (max 10) — không dùng enum để tránh redeploy khi NĐ tái phân vùng.
    /// Tham chiếu hằng số tại <see cref="MPS.Domain.Enums.Compliance.RegionCodes"/>.
    /// </summary>
    public string RegionCode { get; set; } = string.Empty;

    /// <summary>Lương tối thiểu vùng theo tháng (NĐ 293/2025/NĐ-CP).</summary>
    public decimal AmountPerMonth { get; set; }
}
