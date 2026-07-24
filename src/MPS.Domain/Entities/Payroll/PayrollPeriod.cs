using System.ComponentModel.DataAnnotations;
using MPS.Domain.Attributes;
using MPS.Domain.Enums.Payroll;

namespace MPS.Domain.Entities.Payroll;

/// <summary>
/// 1 kỳ lương = (năm, tháng) — hệ thống dùng cho DUY NHẤT 1 công ty nên kỳ không gắn bên ký hợp
/// đồng: mỗi tháng đúng 1 kỳ, phủ MỌI nhân viên có HĐ hiệu lực. (ContractParty chỉ là phiên bản
/// bên ký/người đại diện theo thời kỳ; lọc kỳ lương theo nó sẽ bỏ sót nhân viên.)
/// Mỗi NV 1 dòng + 1 dòng tổng. Khi tạo: snapshot bảng công thức + version compliance + bảng hệ
/// số năng suất để tái lập bất biến.
/// </summary>
[Auditable]
public class PayrollPeriod
{
    public int Id { get; set; }

    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }

    /// <summary>Ngày dùng để lookup compliance version (mặc định = ngày cuối tháng kỳ).</summary>
    public DateOnly PayDate { get; set; }

    /// <summary>Công chuẩn tháng (mẫu số phân bổ thu nhập) — auto đếm ngày làm việc, cho sửa.</summary>
    public decimal StandardWorkingDays { get; set; }

    public PayrollPeriodStatus Status { get; set; } = PayrollPeriodStatus.Draft;

    // ===== Cờ "cần tính lại" (stale) — BS-6/[D1] =====
    /// <summary>
    /// True khi một NGUỒN của kỳ (chấm công, duyệt OT/nghỉ/WFH/đi-muộn, phụ lục HĐ, đánh giá tháng)
    /// đổi SAU khi kỳ đã được tính. Chỉ áp cho kỳ Draft — kỳ Finalized đã đóng băng số liệu.
    /// Xoá cờ bằng POST /payroll/periods/{id}/recalculate.
    /// </summary>
    public bool IsStale { get; set; }

    /// <summary>Lý do gần nhất khiến kỳ bị đánh cờ (nguồn nào đổi).</summary>
    public string? StaleReason { get; set; }

    /// <summary>Thời điểm đánh cờ gần nhất (UTC).</summary>
    public DateTime? StaleMarkedAt { get; set; }

    public int FormulaSetId { get; set; }
    public PayrollFormulaSet FormulaSet { get; set; } = null!;

    // ===== Snapshot bất biến (JSON) =====
    public string FormulaSnapshotJson { get; set; } = string.Empty;
    public string ParamsSnapshotJson { get; set; } = string.Empty;
    public string ProductivitySnapshotJson { get; set; } = string.Empty;

    /// <summary>Biểu thuế TNCN (PitBracketSnapshot) của kỳ — để recompute hàm THUE_LUYTIEN bất biến.</summary>
    public string PitBracketsSnapshotJson { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<PayrollComplianceRef> ComplianceRefs { get; set; } = new List<PayrollComplianceRef>();
    public ICollection<PayrollRow> Rows { get; set; } = new List<PayrollRow>();
    public ICollection<PayrollNote> Notes { get; set; } = new List<PayrollNote>();
}
