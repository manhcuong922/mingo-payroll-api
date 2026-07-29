using System.ComponentModel.DataAnnotations;
using MPS.Domain.Attributes;
using MPS.Domain.Enums.Payroll;

namespace MPS.Domain.Entities.Payroll.BhxhTax;

/// <summary>
/// Chi tiết đóng góp BHXH/TNCN theo từng NV cho 1 <see cref="PaymentObligation"/>.
/// Cột số (<see cref="EmployeeContribution"/>, <see cref="EmployerContribution"/>) là snapshot
/// bất biến từ Payroll Engine tại lúc lock. Các field payment (<see cref="PaidDate"/>,
/// <see cref="PaidAmount"/>, ...) và exclusion (<see cref="ExcludedReason"/>) mutable — thao tác
/// qua command <c>RecordLinePaymentCommand</c>, <c>ExcludeLineCommand</c>, <c>RevertLineCommand</c>.
/// </summary>
[Auditable]
public class PaymentObligationLine
{
    public int Id { get; set; }

    public int PaymentObligationId { get; set; }
    public PaymentObligation PaymentObligation { get; set; } = null!;

    public int EmployeeInformationId { get; set; }
    public EmployeeInformation EmployeeInformation { get; set; } = null!;

    /// <summary>Mã NV tại thời điểm lock — chống rename NV sau này.</summary>
    public string EmployeeCodeSnapshot { get; set; } = string.Empty;

    /// <summary>Họ tên NV tại thời điểm lock.</summary>
    public string EmployeeFullNameSnapshot { get; set; } = string.Empty;

    /// <summary>Phần NLD đóng (BHXH: CONG_BH_NLD; TNCN: THUETNCN).</summary>
    public decimal EmployeeContribution { get; set; }

    /// <summary>Phần DN đóng (BHXH: CONG_BH_DN + KPCD; TNCN: 0).</summary>
    public decimal EmployerContribution { get; set; }

    /// <summary>
    /// Tổng = EmployeeContribution + EmployerContribution. Computed column persisted
    /// trong SQL Server (EF <c>HasComputedColumnSql</c>).
    /// </summary>
    public decimal TotalAmount { get; set; }

    public PaymentLineStatus LineStatus { get; set; } = PaymentLineStatus.Pending;

    // ===== Payment fields (set khi LineStatus = Paid) =====
    public DateTime? PaidDate { get; set; }
    public decimal? PaidAmount { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? TransactionRef { get; set; }
    public string? PaymentNotes { get; set; }

    public int? RecordedByUserId { get; set; }
    public User? RecordedByUser { get; set; }
    public DateTime? RecordedAt { get; set; }

    // ===== Exclusion fields (set khi LineStatus = Excluded) =====
    public string? ExcludedReason { get; set; }
    public int? ExcludedByUserId { get; set; }
    public User? ExcludedByUser { get; set; }
    public DateTime? ExcludedAt { get; set; }

    // ===== Audit =====
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
