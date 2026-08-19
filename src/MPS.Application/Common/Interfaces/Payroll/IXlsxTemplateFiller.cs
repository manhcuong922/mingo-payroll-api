namespace MPS.Application.Common.Interfaces.Payroll;

/// <summary>
/// Một ô cần ghi đè lên biểu mẫu Excel có sẵn. Định dạng của ô (font, khung, kiểu số/ngày)
/// GIỮ NGUYÊN theo mẫu — chỉ thay nội dung, nên biểu mẫu ngân hàng không bị vỡ trình bày.
/// </summary>
public sealed record XlsxCellEdit
{
    private XlsxCellEdit(string sheet, string cell)
    {
        Sheet = sheet;
        Cell = cell;
    }

    public string Sheet { get; }

    /// <summary>Địa chỉ ô kiểu A1, VD "B23".</summary>
    public string Cell { get; }

    public string? Text { get; private init; }
    public decimal? Number { get; private init; }
    public DateTime? Date { get; private init; }

    public static XlsxCellEdit OfText(string sheet, string cell, string? value)
        => new(sheet, cell) { Text = value };

    public static XlsxCellEdit OfNumber(string sheet, string cell, decimal value)
        => new(sheet, cell) { Number = value };

    public static XlsxCellEdit OfDate(string sheet, string cell, DateTime value)
        => new(sheet, cell) { Date = value };
}

/// <summary>
/// Yêu cầu chèn thêm dòng vào giữa biểu mẫu — dùng khi bảng dữ liệu của mẫu chỉ chừa sẵn
/// vài dòng mà danh sách thực tế dài hơn.
/// </summary>
/// <param name="Sheet">Tên sheet.</param>
/// <param name="BeforeRow">Chèn NGAY TRƯỚC dòng này (theo toạ độ biểu mẫu gốc).</param>
/// <param name="Count">Số dòng chèn thêm.</param>
/// <param name="CloneStyleFromRow">
/// Dòng mẫu để sao chép định dạng (khung, nền, kiểu số) sang các dòng mới — thường là dòng
/// dữ liệu cuối cùng mà biểu mẫu đã kẻ sẵn.
/// </param>
public sealed record XlsxRowInsertion(string Sheet, uint BeforeRow, int Count, uint CloneStyleFromRow);

/// <summary>
/// Điền dữ liệu vào biểu mẫu .xlsx có sẵn (khác với các exporter khác trong hệ thống vốn dựng
/// workbook từ số 0). Dùng cho biểu mẫu chính thức của ngân hàng: bố cục, khung, chữ ký, và
/// công thức nội bộ của mẫu phải giữ nguyên tuyệt đối, ta chỉ đổ số vào đúng ô.
/// </summary>
public interface IXlsxTemplateFiller
{
    /// <param name="template">Luồng đọc biểu mẫu gốc (không bị sửa).</param>
    /// <param name="edits">Danh sách ô cần ghi.</param>
    /// <returns>Nội dung file .xlsx đã điền.</returns>
    byte[] Fill(Stream template, IReadOnlyCollection<XlsxCellEdit> edits);

    /// <summary>
    /// Chèn thêm dòng rồi mới ghi ô. Các lần chèn được áp dụng TỪ DƯỚI LÊN theo
    /// <see cref="XlsxRowInsertion.BeforeRow"/>, nên mọi <paramref name="insertions"/> khai
    /// theo toạ độ biểu mẫu GỐC; còn <paramref name="edits"/> phải khai theo toạ độ SAU khi chèn.
    /// </summary>
    byte[] Fill(
        Stream template,
        IReadOnlyCollection<XlsxRowInsertion> insertions,
        IReadOnlyCollection<XlsxCellEdit> edits);
}
