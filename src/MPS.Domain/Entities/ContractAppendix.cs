using ContractEnums = MPS.Domain.Enums.Contract;
using MPS.Domain.Attributes;

namespace MPS.Domain.Entities;

[Auditable]
public class ContractAppendix
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public string AppendixNo { get; set; } = string.Empty;
    public ContractEnums.AppendixType AppendixType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ChangedClausesJson { get; set; } = string.Empty;
    public string AppendixText { get; set; } = string.Empty;
    public DateTime? NoticeSentAt { get; set; }
    public DateTime EffectiveFrom { get; set; }

    /// <summary>
    /// [D12] — Mức lương ĐÓNG BĂNG lúc kích hoạt phụ lục, áp cho các kỳ lương từ
    /// <see cref="EffectiveFrom"/> trở đi. <c>null</c> = phụ lục này không đổi lương.
    ///
    /// Có ô này thì lịch sử lương mới tái lập được: hợp đồng chỉ có MỘT ô
    /// <c>SalaryAmountSnapshot</c> của lần ký gốc, nên nếu không ghi lại mức mới kèm mốc hiệu lực
    /// thì hoặc là tính lại kỳ cũ ra mức mới (sai lịch sử), hoặc là tăng lương không bao giờ vào
    /// bảng lương (đúng hiện trạng trước bản sửa này).
    /// </summary>
    public decimal? SalaryAmountSnapshot { get; set; }

    /// <summary>Đơn vị tiền của <see cref="SalaryAmountSnapshot"/>; kế thừa HĐ gốc.</summary>
    public string? SalaryCurrencySnapshot { get; set; }

    /// <summary>
    /// ⭐ Phase 6 — danh sách phụ cấp của hợp đồng NGAY TRƯỚC khi phụ lục này áp thay đổi
    /// (<c>[{allowanceTypeId, amount, currency}]</c>). Null = phụ lục không đụng tới phụ cấp.
    ///
    /// <para><b>Vì sao lưu trạng thái TRƯỚC chứ không phải SAU:</b> <c>AppendixApplier</c> ghi đè
    /// thẳng <c>ContractAllowances</c> nên trạng thái GỐC lúc ký hợp đồng bị mất ngay từ phụ lục
    /// đầu tiên. Lưu "sau" thì vẫn không tái lập được các kỳ TRƯỚC phụ lục đầu tiên, phải thêm một
    /// ô baseline nữa trên hợp đồng. Lưu "trước" thì tra ngược được toàn bộ lịch sử chỉ với một
    /// cột: phụ cấp tại ngày D = ảnh "trước" của phụ lục CŨ NHẤT có hiệu lực SAU ngày D; không có
    /// phụ lục nào như vậy ⇒ trạng thái hiện tại của hợp đồng.</para>
    ///
    /// <para>Không có ô này thì tính lại một kỳ CŨ sẽ ăn phụ cấp MỚI — sai lịch sử, ngược với lý do
    /// đã làm <c>[D12]</c> cho lương.</para>
    /// </summary>
    public string? AllowanceSnapshotBeforeJson { get; set; }
    public DateTime? SignedAt { get; set; }
    public ContractEnums.AppendixStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
