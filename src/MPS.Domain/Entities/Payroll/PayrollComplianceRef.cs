using MPS.Domain.Enums.Payroll;

namespace MPS.Domain.Entities.Payroll;

/// <summary>
/// Version compliance mà 1 kỳ lương đã dùng (snapshot theo payDate). Dùng để tái lập + diff đổi
/// luật với kỳ trước (sinh PayrollNote LawChange).
/// </summary>
public class PayrollComplianceRef
{
    public int Id { get; set; }

    public int PayrollPeriodId { get; set; }
    public PayrollPeriod PayrollPeriod { get; set; } = null!;

    public ComplianceVersionType VersionType { get; set; }
    public int VersionId { get; set; }
    public DateOnly ValidFrom { get; set; }
    public string LegalReference { get; set; } = string.Empty;

    /// <summary>Scope phụ (AllowanceThreshold theo AllowanceTypeId). Null cho loại global.</summary>
    public int? ScopeId { get; set; }

    /// <summary>Con số tiêu biểu của version (vd trần BHXH, giảm trừ bản thân) — để diff đổi luật.</summary>
    public string? HeadlineLabel { get; set; }
    public decimal? HeadlineValue { get; set; }
}
