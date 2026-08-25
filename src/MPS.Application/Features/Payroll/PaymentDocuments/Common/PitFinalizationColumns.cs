using MPS.Domain.Constants;

namespace MPS.Application.Features.Payroll.PaymentDocuments.Common;

/// <summary>
/// Các cột bảng lương dùng để dựng tờ khai quyết toán thuế TNCN 02/QTT-TNCN.
///
/// <para>Tập <see cref="TaxExempt"/> KHÔNG được đoán: nó phải đúng bằng những cột mà engine
/// trừ ra khỏi <c>TNCT</c> trong <c>DefaultPayrollFormulaSet</c>:</para>
/// <code>
/// TNCT = TONGTHUNHAP − DIENTHOAI − CL_GLT − ANTRUA − PHUTROI_NGAYNGHI − PHUCAP_DEM
/// </code>
/// <para><c>PitFinalizationCalculatorTests</c> khẳng định lại quan hệ này trên dữ liệu thật;
/// nếu sau này luật đổi và engine sửa công thức TNCT thì test đỏ, buộc cập nhật danh sách đây.</para>
/// </summary>
public static class PitFinalizationColumns
{
    /// <summary>Các khoản engine trừ khỏi thu nhập chịu thuế.</summary>
    public static readonly string[] TaxExempt =
    [
        PayrollColumnCodes.DienThoai,
        PayrollColumnCodes.ChenhLechLtg,
        PayrollColumnCodes.ComMienThue,
        PayrollColumnCodes.PhuTroiNgayNghi,
        PayrollColumnCodes.PhuCapDem,
    ];

    /// <summary>
    /// Phần khai vào chỉ tiêu [26] "Thu nhập được miễn thuế khác".
    ///
    /// <para>Chỉ gồm phụ trội làm thêm giờ / làm đêm — luật gọi đích danh là thu nhập ĐƯỢC MIỄN
    /// thuế (Đ.3 TT 111/2013; từ 01/7/2026 miễn toàn bộ tiền tăng ca theo NĐ 253/2026).</para>
    ///
    /// <para>Cố ý KHÔNG gồm <c>DIENTHOAI</c> và <c>ANTRUA</c>: hai khoản này là khoán chi theo
    /// định mức, bản chất "không tính vào thu nhập chịu thuế" chứ không phải "thu nhập được miễn
    /// thuế" — khai vào [26] là đặt sai bản chất khoản thu.</para>
    /// </summary>
    public static readonly string[] ExemptIncomeToDeclare =
    [
        PayrollColumnCodes.ChenhLechLtg,
        PayrollColumnCodes.PhuTroiNgayNghi,
        PayrollColumnCodes.PhuCapDem,
    ];
}
