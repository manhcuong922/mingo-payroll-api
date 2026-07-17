using MPS.Domain.Attributes;

namespace MPS.Domain.Entities.Payroll;

/// <summary>
/// Bảng map điểm đánh giá tháng (MonthlyEvaluation.FinalScore 1–5) → hệ số nhân lương gốc, tính
/// cột PhuCapLuong (P = MucLuong × hệ số). Khoảng [MinScore, MaxScore]. Cấu hình được.
/// </summary>
[Auditable]
public class ProductivityCoefficient
{
    public int Id { get; set; }

    public decimal MinScore { get; set; }
    public decimal MaxScore { get; set; }
    public decimal Coefficient { get; set; }

    public string? Label { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
