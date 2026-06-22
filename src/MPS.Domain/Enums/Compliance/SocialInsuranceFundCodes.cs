// DESIGN NOTE — tại sao KHÔNG dùng enum:
// BHXH có thể thêm/bớt quỹ con (vd NĐ 58/2020 cho phép mức TNLĐ-BNN thấp hơn chuẩn
// theo ngành nghề), nếu luật bổ sung quỹ mới enum buộc redeploy → vi phạm NFR-M05.
// Lưu dạng string trong SocialInsuranceFundEntry.FundCode, validate bằng All.

namespace MPS.Domain.Enums.Compliance;

/// <summary>
/// Mã quỹ thành phần BHXH theo Luật BHXH 41/2024/QH15. Dùng cho seed data và
/// validator cross-checks — giá trị thực lưu dạng string trong DB.
/// </summary>
public static class SocialInsuranceFundCodes
{
    public const string Ht = "HT";           // Hưu trí, tử tuất
    public const string OdTs = "OdTs";       // Ốm đau, thai sản
    public const string TnldBnn = "TnldBnn"; // Tai nạn lao động, bệnh nghề nghiệp

    public static readonly IReadOnlyList<string> All = [Ht, OdTs, TnldBnn];
}
