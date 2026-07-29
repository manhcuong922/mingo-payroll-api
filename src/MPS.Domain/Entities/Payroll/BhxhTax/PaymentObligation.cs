using System.ComponentModel.DataAnnotations;
using MPS.Domain.Attributes;
using MPS.Domain.Enums.Payroll;

namespace MPS.Domain.Entities.Payroll.BhxhTax;

/// <summary>
/// 1 nghĩa vụ nộp cho cơ quan BHXH hoặc Thuế TNCN của một kỳ lương (v3 line-level model).
/// Auto-sinh khi <see cref="PayrollPeriod.Status"/> chuyển sang Locked. Cha giữ tổng
/// <see cref="AmountDue"/> (sum lines tại lúc lock, bất biến) + trạng thái vòng đời +
/// evidence collection. Payment ghi nhận ở từng <see cref="PaymentObligationLine"/>.
/// </summary>
[Auditable]
public class PaymentObligation
{
    public int Id { get; set; }

    public int PayrollPeriodId { get; set; }
    public PayrollPeriod PayrollPeriod { get; set; } = null!;

    public PaymentObligationType ObligationType { get; set; }

    /// <summary>Tổng phải nộp — sum(lines.TotalAmount) tại thời điểm lock. Bất biến.</summary>
    public decimal AmountDue { get; set; }

    /// <summary>Hạn nộp — tính bằng <c>BhxhTaxDeadlineCalculator</c>.</summary>
    public DateTime Deadline { get; set; }

    public PaymentObligationStatus Status { get; set; } = PaymentObligationStatus.Pending;

    /// <summary>Ghi chú tự do cấp cha.</summary>
    public string? Notes { get; set; }

    // ===== Hoàn thành (khi user bấm nút Complete) =====
    public DateTime? CompletedAt { get; set; }
    public int? CompletedByUserId { get; set; }
    public User? CompletedByUser { get; set; }

    // ===== Audit convention =====
    public DateTime CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<PaymentObligationLine> Lines { get; set; } = new List<PaymentObligationLine>();
    public ICollection<PaymentObligationEvidence> Evidences { get; set; } = new List<PaymentObligationEvidence>();
}
