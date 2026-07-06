using System.IO.Compression;
using System.Text;
using MPS.Domain.Entities.Attendance;

namespace MPS.API.Services;

internal static class MonthlyScheduleImportTemplateGenerator
{
    public const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Generate(int year, int month, IReadOnlyCollection<ShiftTemplate> activeShifts)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var lastDayColumn = ColumnName(daysInMonth + 2);
        var referenceStartRow = 38;
        var lastRow = Math.Max(referenceStartRow + 2 + activeShifts.Count, 42);

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", BuildContentTypes());
            WriteEntry(archive, "_rels/.rels", BuildRootRelationships());
            WriteEntry(archive, "xl/workbook.xml", BuildWorkbook());
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships());
            WriteEntry(archive, "xl/styles.xml", BuildStyles());
            WriteEntry(archive, "xl/worksheets/sheet1.xml",
                BuildWorksheet(year, month, daysInMonth, lastDayColumn, referenceStartRow, lastRow, activeShifts));
        }

        return output.ToArray();
    }

    private static string BuildWorksheet(
        int year,
        int month,
        int daysInMonth,
        string lastDayColumn,
        int referenceStartRow,
        int lastRow,
        IReadOnlyCollection<ShiftTemplate> activeShifts)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">""");
        sb.Append($"""<dimension ref="A1:{lastDayColumn}{lastRow}"/>""");
        sb.Append("""<sheetViews><sheetView workbookViewId="0"><pane xSplit="2" ySplit="5" topLeftCell="C6" activePane="bottomRight" state="frozen"/></sheetView></sheetViews>""");
        sb.Append("""<sheetFormatPr defaultRowHeight="18"/>""");
        sb.Append("""<cols>""");
        sb.Append("""<col min="1" max="1" width="15" customWidth="1"/><col min="2" max="2" width="24" customWidth="1"/>""");
        sb.Append($"""<col min="3" max="{daysInMonth + 2}" width="12" customWidth="1"/>""");
        sb.Append("""</cols><sheetData>""");

        WriteRow(sb, 1, [
            InlineCell("A1", $"BẢNG PHÂN CA THÁNG {month:00}/{year}", 1)
        ], height: 26);

        WriteRow(sb, 3, [
            InlineCell("A3", "Ghi chú", 2),
            InlineCell("B3", "Nhập mã NV và mã ca theo danh sách ca bên dưới. Ô trống hoặc OFF/NGHỈ được hiểu là ngày không phân ca.", 7)
        ], height: 32);

        var headerCells = new List<string>
        {
            InlineCell("A5", "Mã NV", 3),
            InlineCell("B5", "Họ tên", 3)
        };
        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(year, month, day);
            var style = IsWeekend(date) ? 4 : 3;
            headerCells.Add(NumberCell($"{ColumnName(day + 2)}5", day, style));
        }
        WriteRow(sb, 5, headerCells, height: 22);

        for (var row = 6; row <= 35; row++)
        {
            var cells = new List<string>
            {
                EmptyCell($"A{row}", 6),
                EmptyCell($"B{row}", 6)
            };
            for (var day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(year, month, day);
                var style = IsWeekend(date) ? 5 : 6;
                cells.Add(EmptyCell($"{ColumnName(day + 2)}{row}", style));
            }
            WriteRow(sb, row, cells);
        }

        WriteRow(sb, referenceStartRow, [
            InlineCell($"A{referenceStartRow}", "CA LÀM HIỆN CÓ", 8)
        ], height: 22);
        WriteRow(sb, referenceStartRow + 1, [
            InlineCell($"A{referenceStartRow + 1}", "Mã ca", 9),
            InlineCell($"B{referenceStartRow + 1}", "Tên ca", 9),
            InlineCell($"C{referenceStartRow + 1}", "Khung giờ", 9),
            InlineCell($"D{referenceStartRow + 1}", "Nghỉ", 9)
        ], height: 20);

        var shiftRow = referenceStartRow + 2;
        foreach (var shift in activeShifts.OrderBy(s => s.Code))
        {
            WriteRow(sb, shiftRow, [
                InlineCell($"A{shiftRow}", shift.Code, 6),
                InlineCell($"B{shiftRow}", shift.Name, 6),
                InlineCell($"C{shiftRow}", $"{FormatTime(shift.StartTime)} - {FormatTime(shift.EndTime)}", 6),
                InlineCell($"D{shiftRow}", FormatBreak(shift), 6)
            ]);
            shiftRow++;
        }

        if (activeShifts.Count == 0)
        {
            WriteRow(sb, shiftRow, [
                InlineCell($"A{shiftRow}", "Chưa có ca active", 6),
                EmptyCell($"B{shiftRow}", 6),
                EmptyCell($"C{shiftRow}", 6),
                EmptyCell($"D{shiftRow}", 6)
            ]);
        }

        sb.Append("""</sheetData>""");
        sb.Append($"""<mergeCells count="3"><mergeCell ref="A1:{lastDayColumn}1"/><mergeCell ref="B3:{lastDayColumn}3"/><mergeCell ref="A{referenceStartRow}:D{referenceStartRow}"/></mergeCells>""");
        sb.Append("""<pageMargins left="0.4" right="0.4" top="0.6" bottom="0.6" header="0.3" footer="0.3"/>""");
        sb.Append("""</worksheet>""");
        return sb.ToString();
    }

    private static void WriteRow(StringBuilder sb, int rowNumber, IReadOnlyCollection<string> cells, int? height = null)
    {
        sb.Append(height.HasValue
            ? $"""<row r="{rowNumber}" ht="{height.Value}" customHeight="1">"""
            : $"""<row r="{rowNumber}">""");
        foreach (var cell in cells) sb.Append(cell);
        sb.Append("</row>");
    }

    private static string InlineCell(string reference, string value, int style) =>
        $"""<c r="{reference}" s="{style}" t="inlineStr"><is><t xml:space="preserve">{Escape(value)}</t></is></c>""";

    private static string NumberCell(string reference, int value, int style) =>
        $"""<c r="{reference}" s="{style}"><v>{value}</v></c>""";

    private static string EmptyCell(string reference, int style) =>
        $"""<c r="{reference}" s="{style}"/>""";

    private static string FormatTime(TimeOnly time) => time.ToString("HH:mm");

    private static string FormatBreak(ShiftTemplate shift) =>
        shift.BreakStartTime is { } start && shift.BreakEndTime is { } end
            ? $"{FormatTime(start)} - {FormatTime(end)}"
            : "-";

    private static bool IsWeekend(DateTime date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private static string ColumnName(int columnIndex)
    {
        var dividend = columnIndex;
        var columnName = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static string Escape(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static string BuildContentTypes() =>
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""";

    private static string BuildRootRelationships() =>
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""";

    private static string BuildWorkbook() =>
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Bảng phân ca" sheetId="1" r:id="rId1"/></sheets></workbook>""";

    private static string BuildWorkbookRelationships() =>
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""";

    private static string BuildStyles() =>
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="4"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font><font><b/><sz val="14"/><color rgb="FF1F4E78"/><name val="Calibri"/></font></fonts><fills count="7"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FFEFEFEF"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FF1F4E78"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFC0504D"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFFFF2CC"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFD9EAD3"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="2"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style="thin"><color rgb="FFD9D9D9"/></left><right style="thin"><color rgb="FFD9D9D9"/></right><top style="thin"><color rgb="FFD9D9D9"/></top><bottom style="thin"><color rgb="FFD9D9D9"/></bottom><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="10"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="3" fillId="0" borderId="0" xfId="0" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf><xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFill="1" applyBorder="1"/><xf numFmtId="0" fontId="2" fillId="3" borderId="1" xfId="0" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf><xf numFmtId="0" fontId="2" fillId="4" borderId="1" xfId="0" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf><xf numFmtId="0" fontId="0" fillId="5" borderId="1" xfId="0" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf><xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf><xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment vertical="center" wrapText="1"/></xf><xf numFmtId="0" fontId="1" fillId="6" borderId="1" xfId="0" applyFill="1" applyBorder="1"/><xf numFmtId="0" fontId="2" fillId="3" borderId="1" xfId="0" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""";
}
