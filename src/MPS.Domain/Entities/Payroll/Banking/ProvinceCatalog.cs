namespace MPS.Domain.Entities.Payroll.Banking;

/// <summary>
/// Danh mục tỉnh/TP theo bản VPBank phát hành kèm mẫu MB01.QT-NVTT/07 — dùng cho cột
/// "Tỉnh/Thành phố" của chi nhánh ngân hàng thụ hưởng.
///
/// <para>Giữ nguyên bản ngân hàng phát hành (danh sách trước sáp nhập 2025) vì đó mới là
/// thứ VPBank đối chiếu; đây là dữ liệu bảng cập nhật được, không phải enum trong code.</para>
/// </summary>
public class ProvinceCatalog
{
    public int Id { get; set; }

    /// <summary>Mã nội bộ ổn định (VD "TP_HO_CHI_MINH") — khoá nghiệp vụ, không in ra lệnh chi.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên tiếng Việt có dấu — dùng để hiển thị khi chọn.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Tên không dấu VIẾT HOA đúng như ngân hàng yêu cầu ghi trên lệnh chi.</summary>
    public string FormName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
