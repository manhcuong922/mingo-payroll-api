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
    public DateTime? SignedAt { get; set; }
    public ContractEnums.AppendixStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
