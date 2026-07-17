namespace MPS.Domain.Entities.Payroll;

/// <summary>
/// Giá trị 1 ô = (dòng, mã cột). Giá trị hiệu lực = <c>IsOverridden ? OverrideValue : ComputedValue</c>.
/// <see cref="ExplanationJson"/> lưu breakdown cách tính để HR hover xem "vì sao ra số này".
/// </summary>
public class PayrollCellValue
{
    public int Id { get; set; }

    public int PayrollRowId { get; set; }
    public PayrollRow PayrollRow { get; set; } = null!;

    public string ColumnCode { get; set; } = string.Empty;

    /// <summary>Kết quả engine tính (cột số/tiền).</summary>
    public decimal? ComputedValue { get; set; }

    /// <summary>Giá trị text (cột Text/flag: MANV, TENNV, FLAGBH...).</summary>
    public string? TextValue { get; set; }

    // ===== Override (sửa tay, ghim) =====
    public decimal? OverrideValue { get; set; }
    public bool IsOverridden { get; set; }
    public int? OverriddenByUserId { get; set; }
    public User? OverriddenByUser { get; set; }
    public DateTime? OverriddenAt { get; set; }

    /// <summary>Breakdown cách tính (ExplanationNode serialize JSON) — render lúc compute.</summary>
    public string? ExplanationJson { get; set; }

    /// <summary>Công thức đã dùng lúc tính (audit/tra cứu).</summary>
    public string? FormulaSnapshotJson { get; set; }
}
