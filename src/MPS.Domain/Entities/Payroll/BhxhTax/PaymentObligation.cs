using System.ComponentModel.DataAnnotations;
using MPS.Domain.Attributes;
using MPS.Domain.Enums.Payroll;

namespace MPS.Domain.Entities.Payroll.BhxhTax;

/// <summary>
/// 1 nghĩa vụ nộp cho cơ quan BHXH hoặc Thuế TNCN của một kỳ lương. Auto-sinh khi
/// <see cref="PayrollPeriod.Status"/> chuyển sang Locked (handler
/// <c>CreatePaymentObligationsOnPeriodLockedHandler</c>). Không tính lại số tiền —
/// <see cref="AmountDue"/> là snapshot bất biến từ Payroll Engine tại thời điểm khoá.
/// Ghi nhận nộp: điền các trường <see cref="PaidDate"/>, <see cref="PaidAmount"/>,
/// <see cref="PaymentMethod"/>, <see cref="TransactionRef"/> và 2 object key ảnh evidence.
/// </summary>
[Auditable]
public class PaymentObligation
{
    public int Id { get; set; }

    public int PayrollPeriodId { get; set; }
    public PayrollPeriod PayrollPeriod { get; set; } = null!;

    public PaymentObligationType ObligationType { get; set; }

    /// <summary>Số tiền phải nộp — snapshot từ cell Total lúc kỳ khoá, không đổi về sau.</summary>
    public decimal AmountDue { get; set; }

    /// <summary>Hạn nộp — tính bằng <c>BhxhTaxDeadlineCalculator</c>.</summary>
    public DateTime Deadline { get; set; }

    public PaymentObligationStatus Status { get; set; } = PaymentObligationStatus.Pending;

    // ===== Ghi nhận nộp (một lần duy nhất trên 1 nghĩa vụ) =====
    public DateTime? PaidDate { get; set; }
    public decimal? PaidAmount { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>Mã giao dịch/số biên lai.</summary>
    public string? TransactionRef { get; set; }

    /// <summary>MinIO object key ảnh biên lai chuyển khoản (JPG/PNG/WEBP).</summary>
    public string? BankReceiptImageObjectKey { get; set; }

    /// <summary>MinIO object key ảnh xác nhận từ cơ quan BHXH/Thuế (JPG/PNG/WEBP).</summary>
    public string? AuthorityConfirmationImageObjectKey { get; set; }

    public string? Notes { get; set; }

    public int? RecordedByUserId { get; set; }
    public User? RecordedByUser { get; set; }
    public DateTime? RecordedAt { get; set; }

    /// <summary>Set khi user xác nhận chấp nhận số chênh lệch (Discrepancy → Confirmed).</summary>
    public int? ConfirmedByUserId { get; set; }
    public User? ConfirmedByUser { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
