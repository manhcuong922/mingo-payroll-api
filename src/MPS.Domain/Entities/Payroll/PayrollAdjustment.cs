using MPS.Domain.Attributes;

namespace MPS.Domain.Entities.Payroll;

/// <summary>
/// BS-5 — khoản truy lĩnh (+) / truy thu (−) sinh khi NGUỒN của kỳ ĐÃ CHỐT (Finalized) đổi sau khi
/// kỳ được chốt. Không tính lại kỳ cũ (đóng băng), chỉ ghi chênh lệch để gom vào KỲ MỚI.
///
/// Chốt PO (2026-07-21) — phương án (A): thuế TNCN không tính lại; chênh lệch = diff THỰC NHẬN
/// giữa nguồn mới và nguồn cũ với công thức đã đóng băng. Ở kỳ mới, cộng thẳng vào THUCNHAN.
///
/// Quy tắc truy thu âm: khi tổng truy thu > THỰC NHẬN kỳ mới, chỉ trừ vừa đủ để THUCNHAN = 0;
/// phần dư giữ AppliedPeriodId=null để kỳ kế tiếp gom tiếp.
/// </summary>
[Auditable]
public class PayrollAdjustment
{
    public int Id { get; set; }

    /// <summary>NV bị ảnh hưởng.</summary>
    public int EmployeeInformationId { get; set; }
    public EmployeeInformation EmployeeInformation { get; set; } = null!;

    /// <summary>Kỳ Finalized bị sửa nguồn — sinh ra khoản chênh lệch này.</summary>
    public int SourcePeriodId { get; set; }
    public PayrollPeriod SourcePeriod { get; set; } = null!;

    /// <summary>Kỳ mới mà khoản này được áp vào (null = chưa áp, đang chờ kỳ mới gom).</summary>
    public int? AppliedPeriodId { get; set; }
    public PayrollPeriod? AppliedPeriod { get; set; }

    /// <summary>
    /// Số tiền chênh lệch (VND). + = truy lĩnh (trả bù NV); − = truy thu (thu lại của NV).
    /// = THUCNHAN(nguồn mới, công thức frozen) − THUCNHAN(nguồn cũ, công thức frozen).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Lý do phát sinh — thường là mô tả thao tác sửa nguồn (VD: "Duyệt OT ngày 15/06/2026").
    /// Tối đa 500 ký tự.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    /// <summary>Mốc được gom vào kỳ mới (null = chưa áp).</summary>
    public DateTime? AppliedAt { get; set; }
}
