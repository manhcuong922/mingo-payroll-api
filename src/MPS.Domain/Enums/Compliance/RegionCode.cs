// DESIGN NOTE — tại sao KHÔNG dùng enum:
// NĐ 293/2025 hiện có 4 vùng (I/II/III/IV), nhưng lịch sử đã có tái phân vùng
// (vd: NĐ 103/2014 → NĐ 122/2015 điều chỉnh địa bàn). Nếu Chính phủ thêm vùng V
// hoặc đổi ký hiệu vùng, enum buộc redeploy → vi phạm NFR-M05.
// Thay vào đó RegionCode lưu dạng string trong MinimumWageRegion.RegionCode (max 10).
// Constants dưới đây dùng cho seed + validator — không phải enum.

namespace MPS.Domain.Enums.Compliance;

// [REMOVED — replaced by string field on MinimumWageRegion]
// public enum RegionCode { I, II, III, IV }

/// <summary>
/// Known region codes per NĐ 293/2025/NĐ-CP. Used for seed data and validator
/// cross-checks only. The actual value stored in DB is a plain string,
/// so new regions introduced by future decrees require no code change or redeploy.
/// </summary>
public static class RegionCodes
{
    public const string I   = "I";
    public const string II  = "II";
    public const string III = "III";
    public const string IV  = "IV";

    public static readonly IReadOnlyList<string> All = [I, II, III, IV];
}
