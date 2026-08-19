namespace MPS.Domain.Entities.Payroll.Banking;

/// <summary>
/// Danh mục ngân hàng theo bản VPBank phát hành kèm mẫu MB01.QT-NVTT/07.
///
/// <para><see cref="Code"/> là tên viết tắt VPBank chấp nhận ghi trên lệnh chi lương
/// (VD "VIETCOMBANK", "CTBC Bank"). Ghi sai tên viết tắt thì ngân hàng từ chối cả lệnh
/// chứ không chỉ một dòng — nên NLĐ chọn từ danh mục này thay vì gõ tay.</para>
/// </summary>
public class BankCatalog
{
    public int Id { get; set; }

    /// <summary>Tên viết tắt VPBank chấp nhận — dùng làm khoá nghiệp vụ, ghi thẳng lên lệnh chi.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên đầy đủ để hiển thị khi chọn.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Ngân hàng nước ngoài — chỉ để nhóm khi hiển thị, không đổi cách ghi lệnh.</summary>
    public bool IsForeign { get; set; }

    public bool IsActive { get; set; } = true;
}
