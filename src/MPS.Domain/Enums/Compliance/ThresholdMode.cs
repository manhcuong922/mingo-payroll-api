// DESIGN NOTE — tại sao KHÔNG dùng enum:
// Hiện tại TT 111/2013 + TT 26/2016 chỉ có 2 cơ chế miễn: FixedAmount (ngưỡng cố định)
// và ActualAllocation (thực tế theo phân bổ). Tuy nhiên nếu luật bổ sung cơ chế tính
// mới (vd: PercentOfSalary, SlidingScale...) thì enum buộc redeploy → vi phạm NFR-M05.
// Lưu dạng string, validate bằng ThresholdModes.All trong FluentValidation.

namespace MPS.Domain.Enums.Compliance;

// [REMOVED — replaced by string field on AllowanceExemptThresholdVersion]
// public enum ThresholdMode { FixedAmount, ActualAllocation }

/// <summary>
/// Known threshold mode values per TT 111/2013/TT-BTC and TT 26/2016/TT-BLĐTBXH.
/// Stored as plain string in DB so future regulatory modes require no redeploy.
/// </summary>
public static class ThresholdModes
{
    /// <summary>Miễn đến mức cố định theo tháng (vd: ăn ca 730,000/tháng).</summary>
    public const string FixedAmount = "FixedAmount";

    /// <summary>Miễn toàn bộ theo thực tế phân bổ (vd: xăng xe, điện thoại theo hóa đơn).</summary>
    public const string ActualAllocation = "ActualAllocation";

    public static readonly IReadOnlyList<string> All = [FixedAmount, ActualAllocation];
}
