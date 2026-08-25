using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using MPS.Application.Common.Interfaces.Payroll;

namespace MPS.Infrastructure.Services.Templates;

/// <summary>
/// Điền dữ liệu vào biểu mẫu .xlsx có sẵn bằng OpenXml.
///
/// <para>Ba điểm phải giữ đúng, sai là ra chứng từ hỏng mà không báo lỗi:</para>
/// <list type="number">
/// <item>Giữ nguyên <c>StyleIndex</c> của ô mẫu — nếu ghi đè, ô ngày/tiền mất định dạng.</item>
/// <item>Ghi chữ dạng <c>InlineString</c> thay vì thêm vào shared string table — không phải
/// đụng tới bảng chuỗi dùng chung, tránh làm lệch chỉ số của các ô khác trong mẫu.</item>
/// <item>Xoá <c>CalcChain</c> và bật tính lại toàn bộ khi mở — mẫu Agribank chạy hoàn toàn
/// bằng công thức trỏ sang sheet dữ liệu, không ép tính lại thì Excel hiển thị giá trị cũ.</item>
/// </list>
/// </summary>
public sealed class XlsxTemplateFiller : IXlsxTemplateFiller
{
    public byte[] Fill(Stream template, IReadOnlyCollection<XlsxCellEdit> edits)
        => Fill(template, [], edits);

    public byte[] Fill(
        Stream template,
        IReadOnlyCollection<XlsxRowInsertion> insertions,
        IReadOnlyCollection<XlsxCellEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(insertions);
        ArgumentNullException.ThrowIfNull(edits);

        // Sao sang MemoryStream ghi được: luồng nguồn là embedded resource (chỉ đọc).
        using var ms = new MemoryStream();
        template.CopyTo(ms);
        ms.Seek(0, SeekOrigin.Begin);

        using (var doc = SpreadsheetDocument.Open(ms, isEditable: true))
        {
            var workbookPart = doc.WorkbookPart
                ?? throw new InvalidOperationException("XLSX_TEMPLATE_CORRUPT: thiếu WorkbookPart.");

            // TỪ DƯỚI LÊN: chèn ở dưới trước thì toạ độ của các lần chèn phía trên vẫn đúng
            // theo biểu mẫu gốc, bên gọi không phải tự cộng trừ chỉ số.
            foreach (var insertion in insertions.OrderByDescending(i => i.BeforeRow))
            {
                var worksheetPart = ResolveWorksheet(workbookPart, insertion.Sheet);

                XlsxRowInserter.Insert(
                    workbookPart, worksheetPart, insertion.Sheet,
                    insertion.BeforeRow, insertion.Count, insertion.CloneStyleFromRow);

                XlsxRowInserter.CloneRowMerges(
                    worksheetPart.Worksheet,
                    templateRow: insertion.CloneStyleFromRow,
                    firstNewRow: insertion.BeforeRow,
                    count: insertion.Count);

                worksheetPart.Worksheet.Save();
            }

            var written = new Dictionary<string, XlsxFormulaCacheWriter.WrittenValue>(StringComparer.Ordinal);

            foreach (var group in edits.GroupBy(e => e.Sheet, StringComparer.OrdinalIgnoreCase))
            {
                var worksheetPart = ResolveWorksheet(workbookPart, group.Key);
                var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()
                    ?? throw new InvalidOperationException(
                        $"XLSX_TEMPLATE_CORRUPT: sheet '{group.Key}' không có SheetData.");

                foreach (var edit in group)
                {
                    written[XlsxFormulaCacheWriter.MakeKey(edit.Sheet, edit.Cell)] = ApplyEdit(sheetData, edit);
                }

                worksheetPart.Worksheet.Save();
            }

            // Sau khi số liệu đã vào sheet dữ liệu: cập nhật luôn cache của các ô công thức trỏ
            // vào đó, để chứng từ hiện đúng ngay cả ở trình xem không tự tính lại công thức.
            XlsxFormulaCacheWriter.Apply(workbookPart, written);

            ForceRecalculationOnOpen(workbookPart);
            workbookPart.Workbook.Save();
        }

        return ms.ToArray();
    }

    private static WorksheetPart ResolveWorksheet(WorkbookPart workbookPart, string sheetName)
    {
        var sheet = workbookPart.Workbook.Descendants<Sheet>().FirstOrDefault(s =>
            string.Equals(s.Name?.Value, sheetName, StringComparison.OrdinalIgnoreCase));

        if (sheet?.Id?.Value is null)
        {
            var available = string.Join(", ",
                workbookPart.Workbook.Descendants<Sheet>().Select(s => s.Name?.Value));
            throw new InvalidOperationException(
                $"XLSX_TEMPLATE_SHEET_NOT_FOUND: biểu mẫu không có sheet '{sheetName}' " +
                $"(hiện có: {available}). Biểu mẫu nhúng có thể đã bị thay bằng bản khác bố cục.");
        }

        return (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);
    }

    /// <returns>Giá trị vừa ghi, để cập nhật cache cho các ô công thức trỏ tới ô này.</returns>
    private static XlsxFormulaCacheWriter.WrittenValue ApplyEdit(SheetData sheetData, XlsxCellEdit edit)
    {
        var (rowIndex, columnIndex) = ParseReference(edit.Cell);
        var row = GetOrCreateRow(sheetData, rowIndex);
        var cell = GetOrCreateCell(row, edit.Cell, columnIndex);

        // Dọn nội dung cũ nhưng KHÔNG đụng StyleIndex — định dạng là của biểu mẫu.
        cell.CellValue = null;
        cell.InlineString = null;
        cell.CellFormula = null;

        if (edit.Date.HasValue)
        {
            // Excel lưu ngày dưới dạng số sê-ri; định dạng hiển thị lấy từ style ô mẫu.
            var serial = edit.Date.Value.ToOADate();
            cell.DataType = null;
            cell.CellValue = new CellValue(
                serial.ToString(System.Globalization.CultureInfo.InvariantCulture));

            return new XlsxFormulaCacheWriter.WrittenValue(serial, null);
        }

        if (edit.Number.HasValue)
        {
            cell.DataType = null;
            cell.CellValue = new CellValue(
                edit.Number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));

            return new XlsxFormulaCacheWriter.WrittenValue((double)edit.Number.Value, null);
        }

        if (!string.IsNullOrEmpty(edit.Text))
        {
            cell.DataType = CellValues.InlineString;
            cell.InlineString = new InlineString(
                new Text(edit.Text) { Space = SpaceProcessingModeValues.Preserve });

            return new XlsxFormulaCacheWriter.WrittenValue(null, edit.Text);
        }

        // Chuỗi rỗng/null ⇒ để ô trống hẳn, không ghi chuỗi "" gây hiểu nhầm là đã khai.
        cell.DataType = null;

        return default;
    }

    private static Row GetOrCreateRow(SheetData sheetData, uint rowIndex)
    {
        var existing = sheetData.Elements<Row>()
            .FirstOrDefault(r => r.RowIndex?.Value == rowIndex);
        if (existing is not null) return existing;

        var row = new Row { RowIndex = rowIndex };

        // OOXML yêu cầu Row xếp tăng dần theo RowIndex trong SheetData.
        var next = sheetData.Elements<Row>()
            .FirstOrDefault(r => r.RowIndex?.Value > rowIndex);

        if (next is null) sheetData.AppendChild(row);
        else sheetData.InsertBefore(row, next);

        return row;
    }

    private static Cell GetOrCreateCell(Row row, string cellReference, uint columnIndex)
    {
        var existing = row.Elements<Cell>().FirstOrDefault(c =>
            string.Equals(c.CellReference?.Value, cellReference, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;

        var cell = new Cell { CellReference = cellReference };

        // Cell cũng phải xếp tăng dần theo cột trong Row.
        var next = row.Elements<Cell>().FirstOrDefault(c =>
            c.CellReference?.Value is not null
            && ParseReference(c.CellReference!.Value!).Column > columnIndex);

        if (next is null) row.AppendChild(cell);
        else row.InsertBefore(cell, next);

        return cell;
    }

    /// <summary>Tách địa chỉ ô kiểu "AB12" thành (dòng 12, cột 28).</summary>
    internal static (uint Row, uint Column) ParseReference(string cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            throw new ArgumentException("Địa chỉ ô rỗng.", nameof(cellReference));
        }

        var reference = cellReference.Trim().ToUpperInvariant();

        uint column = 0;
        var i = 0;
        while (i < reference.Length && reference[i] is >= 'A' and <= 'Z')
        {
            column = column * 26 + (uint)(reference[i] - 'A' + 1);
            i++;
        }

        if (column == 0 || i == reference.Length
            || !uint.TryParse(reference[i..], out var row) || row == 0)
        {
            throw new ArgumentException(
                $"Địa chỉ ô không hợp lệ: '{cellReference}'.", nameof(cellReference));
        }

        return (row, column);
    }

    /// <summary>
    /// Buộc Excel tính lại toàn bộ công thức khi mở file. Bắt buộc với biểu mẫu chạy bằng
    /// công thức: nếu không, ô hiển thị giá trị đã cache từ lúc biểu mẫu được lưu.
    /// </summary>
    private static void ForceRecalculationOnOpen(WorkbookPart workbookPart)
    {
        if (workbookPart.CalculationChainPart is not null)
        {
            workbookPart.DeletePart(workbookPart.CalculationChainPart);
        }

        var calcProps = workbookPart.Workbook.GetFirstChild<CalculationProperties>();
        if (calcProps is null)
        {
            calcProps = new CalculationProperties();
            workbookPart.Workbook.AppendChild(calcProps);
        }

        calcProps.ForceFullCalculation = true;
        calcProps.FullCalculationOnLoad = true;
    }
}
