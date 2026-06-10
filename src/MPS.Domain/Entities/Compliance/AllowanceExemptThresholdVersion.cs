// [CHANGED] ThresholdMode từ enum → string để không buộc redeploy khi luật bổ sung cơ chế tính mới.
// Validator dùng ThresholdModes.All để cross-check giá trị hợp lệ.

namespace MPS.Domain.Entities.Compliance;

public class AllowanceExemptThresholdVersion
{
    public int Id { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool IsArchived { get; set; } = false;

    public int AllowanceTypeId { get; set; }
    public AllowanceType AllowanceType { get; set; } = null!;

    /// <summary>
    /// Cơ chế tính ngưỡng miễn, vd "FixedAmount" hoặc "ActualAllocation".
    /// Lưu string (max 30) — không dùng enum để tránh redeploy khi luật bổ sung cơ chế mới.
    /// Tham chiếu hằng số tại <see cref="MPS.Domain.Enums.Compliance.ThresholdModes"/>.
    /// </summary>
    public string ThresholdMode { get; set; } = string.Empty;

    /// <summary>Null khi ThresholdMode = ActualAllocation.</summary>
    public decimal? MonthlyExemptAmount { get; set; }

    /// <summary>True nếu khoản phụ cấp này được miễn TNCN.</summary>
    public bool AppliesToTax { get; set; }

    /// <summary>True nếu khoản phụ cấp này được miễn BHXH/BHYT/BHTN.</summary>
    public bool AppliesToSocial { get; set; }

    public string LegalReference { get; set; } = string.Empty;
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
