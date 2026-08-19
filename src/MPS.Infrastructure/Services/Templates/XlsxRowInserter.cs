using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace MPS.Infrastructure.Services.Templates;

/// <summary>
/// Chèn thêm dòng vào giữa một sheet có sẵn và dịch chuyển MỌI thứ tham chiếu tới dòng phía dưới.
///
/// <para>Trong một file .xlsx, chỉ số dòng bị nhắc tới ở nhiều nơi rải rác trên nhiều phần XML.
/// Bỏ sót một chỗ thì file vẫn mở được nhưng chứng từ in ra sai — logo đè lên chữ, ngắt trang
/// giữa bảng, vùng in cụt mất phần cuối. Danh sách đầy đủ những chỗ phải dịch:</para>
/// <list type="number">
/// <item><c>Row.RowIndex</c> và <c>Cell.CellReference</c> của mọi dòng phía dưới.</item>
/// <item><c>MergeCell.Reference</c> — các vùng gộp ô.</item>
/// <item><c>SheetDimension</c> — khung kích thước sheet.</item>
/// <item><c>Break.Id</c> trong <c>rowBreaks</c> — ngắt trang thủ công.</item>
/// <item>Defined name, nhất là <c>_xlnm.Print_Area</c> — vùng in.</item>
/// <item><c>Comment.Reference</c> — ô neo chú thích.</item>
/// <item><c>x:Row</c> trong VML — hình chú thích (0-based).</item>
/// <item><c>xdr:row</c> trong drawing — ảnh/logo neo theo ô (0-based).</item>
/// </list>
///
/// <para>⚠️ Nếu biểu mẫu về sau có thêm dataValidation, conditionalFormatting, hyperlink,
/// autoFilter hay table thì các cấu trúc đó CŨNG tham chiếu dòng và lớp này chưa xử lý —
/// <c>SalaryPaymentTemplateShapeTests</c> canh đúng việc đó và sẽ đỏ.</para>
/// </summary>
internal static class XlsxRowInserter
{
    private static readonly Regex CellRefPattern = new(@"^([A-Z]+)(\d+)$", RegexOptions.Compiled);

    public static void Insert(
        WorkbookPart workbookPart,
        WorksheetPart worksheetPart,
        string sheetName,
        uint beforeRow,
        int count,
        uint cloneStyleFromRow)
    {
        if (count <= 0) return;

        var worksheet = worksheetPart.Worksheet;
        var sheetData = worksheet.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException($"XLSX_TEMPLATE_CORRUPT: sheet '{sheetName}' thiếu SheetData.");

        var donor = sheetData.Elements<Row>()
            .FirstOrDefault(r => r.RowIndex?.Value == cloneStyleFromRow)
            ?? throw new InvalidOperationException(
                $"XLSX_TEMPLATE_CORRUPT: không thấy dòng mẫu {cloneStyleFromRow} để sao định dạng.");

        ShiftRowsAndCells(sheetData, beforeRow, count);
        InsertClonedRows(sheetData, donor, beforeRow, count);
        ShiftMergedCells(worksheet, beforeRow, count);
        ShiftRowBreaks(worksheet, beforeRow, count);
        ShiftDimension(worksheet, count);
        ShiftDefinedNames(workbookPart, sheetName, beforeRow, count);
        ShiftComments(worksheetPart, beforeRow, count);
        ShiftVmlAnchors(worksheetPart, beforeRow, count);
        ShiftDrawingAnchors(worksheetPart, beforeRow, count);

        worksheet.Save();
    }

    private static void ShiftRowsAndCells(SheetData sheetData, uint beforeRow, int count)
    {
        // Duyệt từ dưới lên để không tạo hai dòng cùng chỉ số ở bước trung gian.
        var affected = sheetData.Elements<Row>()
            .Where(r => r.RowIndex?.Value >= beforeRow)
            .OrderByDescending(r => r.RowIndex!.Value)
            .ToList();

        foreach (var row in affected)
        {
            var newIndex = row.RowIndex!.Value + (uint)count;
            row.RowIndex = newIndex;

            foreach (var cell in row.Elements<Cell>())
            {
                if (cell.CellReference?.Value is not { } reference) continue;

                var match = CellRefPattern.Match(reference);
                if (match.Success) cell.CellReference = match.Groups[1].Value + newIndex;
            }
        }
    }

    private static void InsertClonedRows(SheetData sheetData, Row donor, uint beforeRow, int count)
    {
        // Dòng mới thừa hưởng định dạng của dòng mẫu (khung, nền, chiều cao) nhưng bỏ hết
        // nội dung — nếu bê cả giá trị thì mọi dòng sẽ mang dữ liệu của dòng mẫu.
        var anchor = sheetData.Elements<Row>()
            .Where(r => r.RowIndex?.Value >= beforeRow + (uint)count)
            .OrderBy(r => r.RowIndex!.Value)
            .FirstOrDefault();

        for (var i = 0; i < count; i++)
        {
            var rowIndex = beforeRow + (uint)i;
            var clone = (Row)donor.CloneNode(true);
            clone.RowIndex = rowIndex;

            foreach (var cell in clone.Elements<Cell>().ToList())
            {
                cell.CellValue = null;
                cell.InlineString = null;
                cell.CellFormula = null;
                cell.DataType = null;

                if (cell.CellReference?.Value is { } reference)
                {
                    var match = CellRefPattern.Match(reference);
                    if (match.Success) cell.CellReference = match.Groups[1].Value + rowIndex;
                }
            }

            if (anchor is null) sheetData.AppendChild(clone);
            else sheetData.InsertBefore(clone, anchor);
        }
    }

    private static void ShiftMergedCells(Worksheet worksheet, uint beforeRow, int count)
    {
        var mergeCells = worksheet.GetFirstChild<MergeCells>();
        if (mergeCells is null) return;

        foreach (var merge in mergeCells.Elements<MergeCell>())
        {
            if (merge.Reference?.Value is not { } reference) continue;

            var parts = reference.Split(':');
            if (parts.Length != 2) continue;

            merge.Reference = ShiftRef(parts[0], beforeRow, count)
                + ":" + ShiftRef(parts[1], beforeRow, count);
        }

        // Vùng gộp CHO các dòng vừa chèn do <see cref="CloneRowMerges"/> lo, gọi riêng sau
        // bước này vì cần biết dòng mẫu để nhân bản.
    }

    /// <summary>
    /// Nhân bản các vùng gộp NẰM TRONG dòng mẫu sang những dòng vừa chèn. Bảng 1 của mẫu VPBank
    /// gộp F:H ở mỗi dòng dữ liệu — thiếu bước này thì các dòng mới lệch cột so với dòng có sẵn.
    /// </summary>
    public static void CloneRowMerges(Worksheet worksheet, uint templateRow, uint firstNewRow, int count)
    {
        if (count <= 0) return;

        var mergeCells = worksheet.GetFirstChild<MergeCells>();
        if (mergeCells is null) return;

        var templates = mergeCells.Elements<MergeCell>()
            .Where(m => IsSingleRowMergeOn(m.Reference?.Value, templateRow))
            .Select(m => m.Reference!.Value!)
            .ToList();

        foreach (var reference in templates)
        {
            var parts = reference.Split(':');
            var startCol = CellRefPattern.Match(parts[0]).Groups[1].Value;
            var endCol = CellRefPattern.Match(parts[1]).Groups[1].Value;

            for (var i = 0; i < count; i++)
            {
                var row = firstNewRow + (uint)i;
                mergeCells.AppendChild(new MergeCell
                {
                    Reference = $"{startCol}{row}:{endCol}{row}",
                });
            }
        }

        mergeCells.Count = (uint)mergeCells.Elements<MergeCell>().Count();
    }

    private static bool IsSingleRowMergeOn(string? reference, uint row)
    {
        if (reference is null) return false;

        var parts = reference.Split(':');
        if (parts.Length != 2) return false;

        var start = CellRefPattern.Match(parts[0]);
        var end = CellRefPattern.Match(parts[1]);

        return start.Success && end.Success
            && uint.Parse(start.Groups[2].Value) == row
            && uint.Parse(end.Groups[2].Value) == row;
    }

    private static void ShiftRowBreaks(Worksheet worksheet, uint beforeRow, int count)
    {
        var rowBreaks = worksheet.GetFirstChild<RowBreaks>();
        if (rowBreaks is null) return;

        foreach (var brk in rowBreaks.Elements<Break>())
        {
            if (brk.Id?.Value >= beforeRow) brk.Id = brk.Id.Value + (uint)count;
        }
    }

    private static void ShiftDimension(Worksheet worksheet, int count)
    {
        var dimension = worksheet.GetFirstChild<SheetDimension>();
        if (dimension?.Reference?.Value is not { } reference) return;

        var parts = reference.Split(':');
        if (parts.Length != 2) return;

        var end = CellRefPattern.Match(parts[1]);
        if (!end.Success) return;

        dimension.Reference = parts[0] + ":" + end.Groups[1].Value
            + (uint.Parse(end.Groups[2].Value) + (uint)count);
    }

    private static void ShiftDefinedNames(
        WorkbookPart workbookPart, string sheetName, uint beforeRow, int count)
    {
        var definedNames = workbookPart.Workbook.GetFirstChild<DefinedNames>();
        if (definedNames is null) return;

        foreach (var definedName in definedNames.Elements<DefinedName>())
        {
            var text = definedName.Text;
            if (string.IsNullOrEmpty(text)) continue;

            // Chỉ đụng tên trỏ vào chính sheet này; bỏ qua tham chiếu đã hỏng (#REF!).
            if (!text.Contains(sheetName, StringComparison.OrdinalIgnoreCase)) continue;
            if (text.Contains("#REF!", StringComparison.Ordinal)) continue;

            definedName.Text = Regex.Replace(text, @"\$([A-Z]+)\$(\d+)", m =>
            {
                var row = uint.Parse(m.Groups[2].Value);
                return row >= beforeRow
                    ? $"${m.Groups[1].Value}${row + (uint)count}"
                    : m.Value;
            });
        }
    }

    private static void ShiftComments(WorksheetPart worksheetPart, uint beforeRow, int count)
    {
        var commentsPart = worksheetPart.WorksheetCommentsPart;
        if (commentsPart?.Comments is null) return;

        foreach (var comment in commentsPart.Comments.Descendants<Comment>())
        {
            if (comment.Reference?.Value is { } reference)
            {
                comment.Reference = ShiftRef(reference, beforeRow, count);
            }
        }

        commentsPart.Comments.Save();
    }

    /// <summary>
    /// VML giữ hình của chú thích, neo bằng <c>&lt;x:Row&gt;</c> ĐẾM TỪ 0. Không có API typed
    /// nên phải sửa thẳng XML.
    /// </summary>
    private static void ShiftVmlAnchors(WorksheetPart worksheetPart, uint beforeRow, int count)
    {
        var vmlPart = worksheetPart.VmlDrawingParts.FirstOrDefault();
        if (vmlPart is null) return;

        string xml;
        using (var reader = new StreamReader(vmlPart.GetStream(FileMode.Open, FileAccess.Read)))
        {
            xml = reader.ReadToEnd();
        }

        var shifted = Regex.Replace(xml, @"<x:Row>(\d+)</x:Row>", m =>
        {
            var zeroBased = uint.Parse(m.Groups[1].Value);
            return zeroBased + 1 >= beforeRow
                ? $"<x:Row>{zeroBased + (uint)count}</x:Row>"
                : m.Value;
        });

        if (shifted == xml) return;

        using var writer = new StreamWriter(vmlPart.GetStream(FileMode.Create, FileAccess.Write));
        writer.Write(shifted);
    }

    /// <summary>Ảnh/logo neo theo ô bằng <c>&lt;xdr:row&gt;</c>, cũng ĐẾM TỪ 0.</summary>
    private static void ShiftDrawingAnchors(WorksheetPart worksheetPart, uint beforeRow, int count)
    {
        var drawingsPart = worksheetPart.DrawingsPart;
        if (drawingsPart?.WorksheetDrawing is null) return;

        foreach (var rowId in drawingsPart.WorksheetDrawing.Descendants<Xdr.RowId>())
        {
            if (!uint.TryParse(rowId.Text, out var zeroBased)) continue;
            if (zeroBased + 1 < beforeRow) continue;

            rowId.Text = (zeroBased + (uint)count).ToString();
        }

        drawingsPart.WorksheetDrawing.Save();
    }

    private static string ShiftRef(string reference, uint beforeRow, int count)
    {
        var match = CellRefPattern.Match(reference);
        if (!match.Success) return reference;

        var row = uint.Parse(match.Groups[2].Value);
        return row >= beforeRow
            ? match.Groups[1].Value + (row + (uint)count)
            : reference;
    }
}
