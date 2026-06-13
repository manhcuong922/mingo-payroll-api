// DESIGN NOTE — tại sao KHÔNG dùng enum:
// BLLĐ 2019 Điều 98 hiện có 4 loại OT. Nếu luật bổ sung loại mới thì enum
// buộc redeploy → vi phạm NFR-M05. OtType lưu string trong OtRateEntry;
// các hằng số dưới đây dùng cho seed + validator + engine — không phải enum.

namespace MPS.Domain.Enums.Compliance;

/// <summary>
/// Known OT type keys per BLLĐ 2019 Điều 98. Stored as plain string in OtRateEntry.OtType
/// so future legislative additions require no code change or redeploy.
/// </summary>
public static class OtRateTypes
{
    /// <summary>OT ngày thường — floor ≥ 1.50 (Điều 98 khoản 1a).</summary>
    public const string Weekday = "Weekday";

    /// <summary>OT cuối tuần — floor ≥ 2.00 (Điều 98 khoản 1b).</summary>
    public const string Weekend = "Weekend";

    /// <summary>OT ngày lễ/nghỉ bù — floor ≥ 3.00 (Điều 98 khoản 1c).</summary>
    public const string Holiday = "Holiday";

    /// <summary>Làm ban đêm 22:00–06:00 — floor ≥ 1.30 (Điều 98 khoản 2).</summary>
    public const string NightShift = "NightShift";

    /// <summary>
    /// Tất cả loại bắt buộc phải có trong mỗi OtRateVersion.
    /// Validator dùng để kiểm tra completeness; engine dùng để tra cứu rate.
    /// </summary>
    public static readonly IReadOnlyList<string> Required = [Weekday, Weekend, Holiday, NightShift];

    /// <summary>
    /// Floor tối thiểu theo luật cho mỗi loại. Validator kiểm tra Rate >= floor.
    /// Key = OtType string, Value = minimum rate.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, decimal> MinimumRates =
        new Dictionary<string, decimal>
        {
            [Weekday]   = 1.50m,
            [Weekend]   = 2.00m,
            [Holiday]   = 3.00m,
            [NightShift] = 1.30m,
        };
}
