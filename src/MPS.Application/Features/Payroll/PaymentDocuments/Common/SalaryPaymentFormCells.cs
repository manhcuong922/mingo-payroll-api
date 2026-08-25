namespace MPS.Application.Features.Payroll.PaymentDocuments.Common;

/// <summary>
/// Toạ độ ô trên mẫu "Đề nghị thanh toán lương kiêm ủy nhiệm chi" của VPBank (MB01.QT-NVTT/07).
///
/// <para>Khác mẫu Agribank, mẫu này KHÔNG có công thức: nhãn và giá trị nằm chung một ô
/// (VD ô C4 chứa sẵn "Số chứng từ*: ………"). Nên khi ghi phải ghi lại cả nhãn — xem
/// <see cref="LabelledField"/>.</para>
///
/// <para>⚠️ Bảng danh sách NV chỉ được kẻ sẵn 3 dòng mỗi bảng; dài hơn thì phải CHÈN dòng và
/// dịch toàn bộ phần bên dưới. <c>SalaryPaymentTemplateShapeTests</c> canh việc biểu mẫu nhúng
/// vẫn đúng hình dạng mà các hằng số này giả định.</para>
/// </summary>
public static class SalaryPaymentFormCells
{
    public const string Sheet = "Lệnh TT";

    /// <summary>
    /// Một ô chứa cả nhãn lẫn giá trị.
    ///
    /// <para><see cref="Label"/> phải là ĐÚNG chữ biểu mẫu đang in ở ô đó (không kèm dấu hai
    /// chấm nếu mẫu không có) — <c>SalaryPaymentTemplateShapeTests</c> đối chiếu thẳng với biểu
    /// mẫu nhúng, nên hằng số lệch chữ là test đỏ ngay.</para>
    /// </summary>
    public sealed record LabelledField(string Cell, string Label)
    {
        public string Render(string? value)
        {
            var label = Label.TrimEnd();
            var separator = label.EndsWith(':') ? " " : ": ";
            return label + separator + (value ?? string.Empty);
        }
    }

    // ===== Khối đầu chứng từ =====
    public static readonly LabelledField DocumentNo = new("C4", "Số chứng từ*: ");
    public static readonly LabelledField IssueDate = new("E4", "Ngày lập*: ");
    public static readonly LabelledField Branch = new("A6",
        "Kính gửi: Ngân hàng TMCP Việt Nam Thịnh Vượng (VPBank) - Chi nhánh*: ");

    // ===== I. Thông tin bên đề nghị chi lương =====
    public static readonly LabelledField CompanyName = new("A9", "Tên Doanh nghiệp*: ");
    public static readonly LabelledField CustomerCif = new("G9", "Mã KH (CIF): ");
    public static readonly LabelledField CompanyAddress = new("A10", "Địa chỉ*: ");
    public static readonly LabelledField CompanyPhone = new("A11", "Điện thoại liên lạc : ");

    // ===== II. Phương thức chi lương =====
    public static readonly LabelledField DebitAccount = new("A18", "Tài khoản trích nợ và thu phí: ");
    public static readonly LabelledField Currency = new("D18", "Loại tiền: ");

    // ===== III. Thông tin chi lương =====
    public static readonly LabelledField TotalFigures = new("B22", "Bằng số: ");
    public static readonly LabelledField TotalWords = new("B23", "Bằng chữ: ");
    public static readonly LabelledField ValueDate = new("A24",
        "Ngày Lệnh chi lương có giá trị thanh toán*: ");
    public static readonly LabelledField PaymentContent = new("A25", "Nội dung thanh toán*: ");

    // ===== Bảng 1 — NV có tài khoản tại ngân hàng chủ quản =====
    public const uint Table1HeaderRow = 29;
    public const uint Table1FirstDataRow = 30;
    public const uint Table1LastPrebakedRow = 32;

    // ===== Bảng 2 — NV có tài khoản ngân hàng khác =====
    public const uint Table2TitleRow = 33;
    public const uint Table2HeaderRow = 34;
    public const uint Table2FirstDataRow = 35;
    public const uint Table2LastPrebakedRow = 37;

    // ===== Dòng tổng cộng =====
    public const uint TotalCountRow = 38;
    public const uint TotalAmountRow = 39;
    public const string TotalAmountCell = "B38";
    // Hai nhãn này trên mẫu KHÔNG có dấu hai chấm — Render tự thêm khi ghi.
    public static readonly LabelledField TotalCountWords = new("D38", "Số món (Bằng chữ)");
    public static readonly LabelledField TotalAmountWords = new("D39", "Số tiền (Bằng chữ)");

    /// <summary>Số dòng dữ liệu mỗi bảng đã được kẻ sẵn trong biểu mẫu.</summary>
    public const int PrebakedRowsPerTable = 3;

    // ===== Cột trong bảng danh sách =====
    public const string ColOrdinal = "A";
    public const string ColFullName = "B";
    public const string ColAccountNo = "C";
    public const string ColAmount = "D";
    public const string ColAddress = "E";
    public const string ColBank = "F";
    public const string ColBranch = "G";       // chỉ bảng 2
    public const string ColProvince = "H";     // chỉ bảng 2
    public const string ColNote = "I";
}
