using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace MPS.Infrastructure.Services.Templates;

/// <summary>
/// Ghi lại GIÁ TRỊ CACHE cho những ô công thức trỏ vào các ô vừa được điền.
///
/// <para>Vì sao cần: biểu mẫu ủy nhiệm chi Agribank có sheet in <c>UNC</c> chạy hoàn toàn bằng
/// công thức trỏ về sheet dữ liệu <c>Nguon</c>, và file mẫu gốc được lưu kèm cache toàn <c>0</c>.
/// Đặt cờ tính lại khi mở (xem <c>XlsxTemplateFiller.ForceRecalculationOnOpen</c>) chỉ cứu được
/// Excel bản desktop; mọi thứ đọc thẳng cache — xem trước của trình duyệt/OneDrive/Google Drive,
/// LibreOffice ở chế độ mặc định "không tính lại khi mở", thư viện đọc xlsx — vẫn hiện chứng từ
/// TOÀN SỐ 0. Ghi cache đúng thì cả hai phía đều ra một kết quả.</para>
///
/// <para>Chỉ đụng vào ô công thức có tham chiếu tới ô VỪA GHI. Công thức nội bộ của biểu mẫu
/// (không liên quan dữ liệu ta đổ vào) giữ nguyên cache do người lập mẫu lưu — xoá đi là làm
/// hỏng những ô vốn đang đúng.</para>
///
/// <para>Công thức nào hiểu được thì ghi cache; không hiểu được mà CÓ tham chiếu ô vừa ghi thì
/// XOÁ cache. Để nguyên là hiện số cũ — với chứng từ chuyển tiền, ô trống còn hơn số sai.</para>
/// </summary>
internal static class XlsxFormulaCacheWriter
{
    /// <summary>Giá trị đã ghi vào một ô, dùng làm đầu vào khi tính lại cache.</summary>
    /// <param name="Number">Số (kể cả ngày dạng số sê-ri Excel).</param>
    /// <param name="Text">Chuỗi.</param>
    /// <remarks>Cả hai cùng null ⇒ ô được ghi thành TRỐNG, không suy ra được cache.</remarks>
    internal readonly record struct WrittenValue(double? Number, string? Text)
    {
        public bool IsBlank => Number is null && Text is null;
    }

    /// <summary>Tham chiếu kiểu <c>Nguon!$B$11</c> hoặc <c>'Sheet có dấu cách'!B11</c>.</summary>
    private static readonly Regex ReferencePattern = new(
        @"(?:'(?<sheet>[^']+)'|(?<sheet>[A-Za-z0-9_.]+))!\$?(?<col>[A-Za-z]{1,3})\$?(?<row>\d+)",
        RegexOptions.Compiled);

    /// <summary>Toàn bộ operand là lời gọi <c>TEXT(&lt;tham chiếu&gt;;"định dạng")</c>.</summary>
    private static readonly Regex TextCallPattern = new(
        @"^TEXT\(\s*(?<ref>[^,;]+?)\s*[,;]\s*""(?<format>[^""]*)""\s*\)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <param name="written">
    /// Giá trị vừa ghi, khoá theo <see cref="MakeKey"/>. Rỗng ⇒ không làm gì.
    /// </param>
    public static void Apply(
        WorkbookPart workbookPart,
        IReadOnlyDictionary<string, WrittenValue> written)
    {
        if (written.Count == 0) return;

        foreach (var sheet in workbookPart.Workbook.Descendants<Sheet>().ToList())
        {
            if (sheet.Id?.Value is null) continue;
            if (workbookPart.GetPartById(sheet.Id!.Value!) is not WorksheetPart worksheetPart) continue;

            var touched = false;

            foreach (var cell in worksheetPart.Worksheet.Descendants<Cell>().ToList())
            {
                var formula = cell.CellFormula?.Text;
                if (string.IsNullOrWhiteSpace(formula)) continue;
                if (!ReferencesWrittenCell(formula, written)) continue;

                if (TryEvaluate(formula, written, out var value))
                {
                    WriteCache(cell, value);
                }
                else
                {
                    ClearCache(cell);
                }

                touched = true;
            }

            if (touched) worksheetPart.Worksheet.Save();
        }
    }

    internal static string MakeKey(string sheet, string cell)
        => $"{sheet.Trim().ToUpperInvariant()}!{cell.Trim().ToUpperInvariant()}";

    /// <summary>
    /// Công thức có chạm tới ô nào vừa ghi không. Quét thô cả trong chuỗi hằng — chấp nhận
    /// dương tính giả vì hậu quả tệ nhất chỉ là tính lại cache của ô vốn đã đúng.
    /// </summary>
    private static bool ReferencesWrittenCell(
        string formula, IReadOnlyDictionary<string, WrittenValue> written)
    {
        foreach (Match match in ReferencePattern.Matches(formula))
        {
            if (written.ContainsKey(KeyOf(match))) return true;
        }

        return false;
    }

    private static string KeyOf(Match match)
        => MakeKey(match.Groups["sheet"].Value, match.Groups["col"].Value + match.Groups["row"].Value);

    /// <summary>
    /// Tính giá trị của các công thức đơn giản mà biểu mẫu ngân hàng dùng: một tham chiếu, hoặc
    /// chuỗi nối bằng <c>&amp;</c> giữa hằng chuỗi / tham chiếu / <c>TEXT(...)</c>. Gặp bất cứ
    /// dạng nào khác thì trả false — bên gọi sẽ xoá cache thay vì đoán bừa.
    /// </summary>
    private static bool TryEvaluate(
        string formula,
        IReadOnlyDictionary<string, WrittenValue> written,
        out WrittenValue value)
    {
        value = default;

        if (!TrySplitConcatenation(formula.Trim(), out var operands)) return false;
        if (operands.Count == 0) return false;

        // Một tham chiếu đứng một mình ⇒ giữ nguyên KIỂU của ô nguồn (số vẫn là số, để ô tiền
        // trên bản in còn ăn được định dạng phân tách hàng nghìn của biểu mẫu).
        if (operands.Count == 1)
        {
            return TryEvaluateOperand(operands[0], written, out value) && !value.IsBlank;
        }

        var builder = new StringBuilder();

        foreach (var operand in operands)
        {
            if (!TryEvaluateOperand(operand, written, out var part)) return false;
            if (part.IsBlank) return false;

            builder.Append(ToText(part));
        }

        value = new WrittenValue(null, builder.ToString());
        return true;
    }

    private static bool TryEvaluateOperand(
        string operand,
        IReadOnlyDictionary<string, WrittenValue> written,
        out WrittenValue value)
    {
        value = default;
        operand = operand.Trim();

        if (operand.Length == 0) return false;

        // Hằng chuỗi: "abc" (dấu nháy kép đôi bên trong là một dấu nháy kép).
        if (operand.Length >= 2 && operand[0] == '"' && operand[^1] == '"')
        {
            value = new WrittenValue(null, operand[1..^1].Replace("\"\"", "\""));
            return true;
        }

        var textCall = TextCallPattern.Match(operand);
        if (textCall.Success)
        {
            if (!TryResolveReference(textCall.Groups["ref"].Value, written, out var source)) return false;

            return TryFormat(source, textCall.Groups["format"].Value, out value);
        }

        return TryResolveReference(operand, written, out value);
    }

    private static bool TryResolveReference(
        string reference,
        IReadOnlyDictionary<string, WrittenValue> written,
        out WrittenValue value)
    {
        value = default;

        var match = ReferencePattern.Match(reference.Trim());

        // Phải khớp TRỌN operand, nếu không thì đây là biểu thức phức tạp hơn ta hiểu được.
        if (!match.Success || match.Index != 0 || match.Length != reference.Trim().Length) return false;

        return written.TryGetValue(KeyOf(match), out value);
    }

    /// <summary>
    /// Mô phỏng hàm TEXT của Excel ở đúng phạm vi biểu mẫu cần: chuỗi thì Excel trả nguyên chuỗi,
    /// số thì hiểu là ngày dạng sê-ri khi định dạng chỉ gồm ký tự ngày tháng.
    /// </summary>
    private static bool TryFormat(WrittenValue source, string excelFormat, out WrittenValue value)
    {
        value = default;

        if (source.Text is not null)
        {
            value = new WrittenValue(null, source.Text);
            return true;
        }

        if (source.Number is null) return false;

        if (!excelFormat.All(c => c is 'd' or 'D' or 'm' or 'M' or 'y' or 'Y' or '/' or '-' or '.' or ' '))
        {
            return false;
        }

        // Trong định dạng ngày của Excel, 'm' là THÁNG; .NET dùng 'M' ('m' của .NET là phút).
        var netFormat = excelFormat.Replace('m', 'M');

        try
        {
            value = new WrittenValue(
                null,
                DateTime.FromOADate(source.Number.Value).ToString(netFormat, CultureInfo.InvariantCulture));
            return true;
        }
        catch (ArgumentException)
        {
            // Số sê-ri nằm ngoài dải ngày hợp lệ của Excel.
            return false;
        }
    }

    /// <summary>Tách chuỗi nối theo <c>&amp;</c> ở mức ngoài cùng, bỏ qua & nằm trong "..." hoặc (...).</summary>
    private static bool TrySplitConcatenation(string formula, out List<string> operands)
    {
        operands = [];

        var current = new StringBuilder();
        var depth = 0;

        for (var i = 0; i < formula.Length; i++)
        {
            var c = formula[i];

            if (c == '"')
            {
                if (!TryCopyQuotedLiteral(formula, ref i, current)) return false;
                continue;
            }

            if (c == '(') depth++;
            else if (c == ')' && --depth < 0) return false;
            else if (c == '&' && depth == 0)
            {
                operands.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (depth != 0) return false;

        operands.Add(current.ToString());

        return operands.All(o => o.Trim().Length > 0);
    }

    /// <summary>
    /// Sao nguyên hằng chuỗi <c>"..."</c> sang <paramref name="target"/> và đẩy
    /// <paramref name="i"/> tới dấu nháy đóng. False = chuỗi thiếu dấu đóng.
    /// </summary>
    private static bool TryCopyQuotedLiteral(string formula, ref int i, StringBuilder target)
    {
        target.Append(formula[i]);

        for (i++; i < formula.Length; i++)
        {
            var c = formula[i];
            target.Append(c);

            if (c != '"') continue;

            // "" bên trong chuỗi là ký tự nháy kép, không phải dấu kết thúc chuỗi.
            if (i + 1 < formula.Length && formula[i + 1] == '"')
            {
                target.Append(formula[++i]);
                continue;
            }

            return true;
        }

        return false;
    }

    private static string ToText(WrittenValue value)
        => value.Text ?? value.Number!.Value.ToString("0.##########", CultureInfo.InvariantCulture);

    private static void WriteCache(Cell cell, WrittenValue value)
    {
        // Ô công thức không được mang InlineString — cache của nó là <v>, kiểu "str" nếu là chuỗi.
        cell.InlineString = null;

        if (value.Text is not null)
        {
            cell.DataType = CellValues.String;
            cell.CellValue = new CellValue(value.Text);
            return;
        }

        cell.DataType = null;
        cell.CellValue = new CellValue(
            value.Number!.Value.ToString("0.##########", CultureInfo.InvariantCulture));
    }

    private static void ClearCache(Cell cell)
    {
        cell.InlineString = null;
        cell.CellValue = null;
        cell.DataType = null;
    }
}
