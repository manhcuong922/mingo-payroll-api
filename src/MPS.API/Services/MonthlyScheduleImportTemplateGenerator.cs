using System.IO.Compression;
using System.Text;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.API.Services;

internal static class MonthlyScheduleImportTemplateGenerator
{
    public const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const int DemoRowCount = 3;
    private const int TotalDataRows = 10; // 3 demo + 7 rỗng
    private const int FirstDataRow = 5;   // R5

    /// <summary>
    /// Static demo rows theo mode. HR sẽ thay bằng dữ liệu thật khi điền.
    /// Dept mode dùng mã phòng ban giống seed default (IT/KT/NS) — đọc user nhận ra ngay.
    /// </summary>
    private static readonly (string Code, string Name)[] EmployeeDemoRows =
    {
        ("EMP001", "Nguyễn Văn An"),
        ("EMP002", "Trần Thị Bình"),
        ("EMP003", "Lê Văn Cường"),
    };

    private static readonly (string Code, string Name)[] DepartmentDemoRows =
    {
        ("IT", "Phòng Công nghệ thông tin"),
        ("KT", "Phòng Kế toán"),
        ("NS", "Phòng Nhân sự"),
    };

    public static byte[] Generate(
        int year,
        int month,
        IReadOnlyCollection<ShiftTemplate> activeShifts,
        ScheduleApplyTarget applyTarget = ScheduleApplyTarget.Employees)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var lastDayColumn = ColumnName(daysInMonth + 2);
        // Layout: R1 title, R3 day-numbers, R4 weekday, R5..(R5+DataRows-1) data, gap 1 row, sau đó "Ca làm" section.
        var lastDataRow = FirstDataRow + TotalDataRows - 1;         // R14
        var referenceStartRow = lastDataRow + 2;                     // R16
        var lastRow = referenceStartRow + 2 + Math.Max(activeShifts.Count, 1);

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", BuildContentTypes());
            WriteEntry(archive, "_rels/.rels", BuildRootRelationships());
            WriteEntry(archive, "xl/workbook.xml", BuildWorkbook());
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships());
            WriteEntry(archive, "xl/styles.xml", BuildStyles());
            WriteEntry(archive, "xl/worksheets/sheet1.xml",
                BuildWorksheet(year, month, daysInMonth, lastDayColumn, referenceStartRow, lastRow, activeShifts, applyTarget));
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
        IReadOnlyCollection<ShiftTemplate> activeShifts,
        ScheduleApplyTarget applyTarget)
    {
        var isDeptMode = applyTarget == ScheduleApplyTarget.Departments;
        var codeHeader = isDeptMode ? "Mã phòng ban" : "Mã NV";
        var nameHeader = isDeptMode ? "Tên phòng ban" : "Họ tên";
        var demoRows = isDeptMode ? DepartmentDemoRows : EmployeeDemoRows;
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">""");
        sb.Append($"""<dimension ref="A1:{lastDayColumn}{lastRow}"/>""");
        // Freeze 2 cột đầu + 4 hàng đầu (title + day-numbers + weekday) → data cuộn ngang/dọc mượt.
        sb.Append("""<sheetViews><sheetView workbookViewId="0"><pane xSplit="2" ySplit="4" topLeftCell="C5" activePane="bottomRight" state="frozen"/></sheetView></sheetViews>""");
        sb.Append("""<sheetFormatPr defaultRowHeight="18"/>""");
        sb.Append("""<cols>""");
        sb.Append("""<col min="1" max="1" width="14" customWidth="1"/><col min="2" max="2" width="24" customWidth="1"/>""");
        // Day cols đều width 10 → chứa vừa mã ca thông dụng ("CA-CHIEU-1", "FULLTIME", ...).
        sb.Append($"""<col min="3" max="{daysInMonth + 2}" width="10" customWidth="1"/>""");
        sb.Append("""</cols><sheetData>""");

        // R1 — Title lớn, xanh đậm
        var titleSuffix = isDeptMode ? " (theo phòng ban)" : "";
        WriteRow(sb, 1, [
            InlineCell("A1", $"BẢNG PHÂN CA THÁNG {month:00}/{year}{titleSuffix}", 1)
        ], height: 40);

        // R3 — Day numbers header (code+name theo mode, 1, 2, ..., N)
        var dayNumberCells = new List<string>
        {
            InlineCell("A3", codeHeader, 3),
            InlineCell("B3", nameHeader, 3)
        };
        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(year, month, day);
            var style = IsWeekend(date) ? 4 : 3;
            dayNumberCells.Add(NumberCell($"{ColumnName(day + 2)}3", day, style));
        }
        WriteRow(sb, 3, dayNumberCells, height: 24);

        // R4 — Weekday header (T2/T3.../CN). A4/B4 bỏ trống vì đã merge với A3/B3.
        var weekdayCells = new List<string>();
        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(year, month, day);
            var style = IsWeekend(date) ? 11 : 10;
            weekdayCells.Add(InlineCell($"{ColumnName(day + 2)}4", ShortWeekday(date.DayOfWeek), style));
        }
        WriteRow(sb, 4, weekdayCells, height: 18);

        // Data rows — 3 demo + (TotalDataRows - 3) trống
        var demoShiftCode = activeShifts.OrderBy(s => s.Code).FirstOrDefault()?.Code ?? "CA-1";

        var lastDataRow = FirstDataRow + TotalDataRows - 1;
        for (var row = FirstDataRow; row <= lastDataRow; row++)
        {
            var demoIndex = row - FirstDataRow;
            var isDemo = demoIndex < DemoRowCount;

            var cells = new List<string>
            {
                isDemo
                    ? InlineCell($"A{row}", demoRows[demoIndex].Code, 6)
                    : EmptyCell($"A{row}", 6),
                isDemo
                    ? InlineCell($"B{row}", demoRows[demoIndex].Name, 6)
                    : EmptyCell($"B{row}", 6),
            };
            for (var day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(year, month, day);
                var isWeekend = IsWeekend(date);
                var style = isWeekend ? 5 : 6;
                var cellRef = $"{ColumnName(day + 2)}{row}";
                cells.Add(isDemo && !isWeekend
                    ? InlineCell(cellRef, demoShiftCode, style)
                    : EmptyCell(cellRef, style));
            }
            WriteRow(sb, row, cells);
        }

        // Section title "Ca làm" merge A:E (bao cả cột Nghỉ ở col E)
        WriteRow(sb, referenceStartRow, [
            InlineCell($"A{referenceStartRow}", "Ca làm", 8)
        ], height: 22);
        WriteRow(sb, referenceStartRow + 1, [
            InlineCell($"A{referenceStartRow + 1}", "Mã ca", 9),
            InlineCell($"B{referenceStartRow + 1}", "Tên ca", 9),
            InlineCell($"C{referenceStartRow + 1}", "Khung giờ", 9),
            EmptyCell($"D{referenceStartRow + 1}", 9),
            InlineCell($"E{referenceStartRow + 1}", "Nghỉ", 9)
        ], height: 20);

        var shiftRow = referenceStartRow + 2;
        var refDataRowMerges = new StringBuilder();
        refDataRowMerges.Append($"""<mergeCell ref="C{referenceStartRow + 1}:D{referenceStartRow + 1}"/>""");

        foreach (var shift in activeShifts.OrderBy(s => s.Code))
        {
            WriteRow(sb, shiftRow, [
                InlineCell($"A{shiftRow}", shift.Code, 6),
                InlineCell($"B{shiftRow}", shift.Name, 6),
                InlineCell($"C{shiftRow}", $"{FormatTime(shift.StartTime)} - {FormatTime(shift.EndTime)}", 6),
                EmptyCell($"D{shiftRow}", 6),
                InlineCell($"E{shiftRow}", FormatBreak(shift), 6)
            ]);
            refDataRowMerges.Append($"""<mergeCell ref="C{shiftRow}:D{shiftRow}"/>""");
            shiftRow++;
        }

        if (activeShifts.Count == 0)
        {
            WriteRow(sb, shiftRow, [
                InlineCell($"A{shiftRow}", "Chưa có ca active", 6),
                EmptyCell($"B{shiftRow}", 6),
                EmptyCell($"C{shiftRow}", 6),
                EmptyCell($"D{shiftRow}", 6),
                EmptyCell($"E{shiftRow}", 6)
            ]);
            refDataRowMerges.Append($"""<mergeCell ref="C{shiftRow}:D{shiftRow}"/>""");
            shiftRow++;
        }

        // Tổng merges: 3 cố định (title, A3:A4, B3:B4) + 1 section title (A:E) + N merges C:D cho ref rows
        var refMergeCount = 1 + (activeShifts.Count == 0 ? 1 : activeShifts.Count); // header + data rows
        var totalMergeCount = 3 + 1 + refMergeCount;

        sb.Append("""</sheetData>""");
        sb.Append($"""<mergeCells count="{totalMergeCount}"><mergeCell ref="A1:{lastDayColumn}1"/><mergeCell ref="A3:A4"/><mergeCell ref="B3:B4"/><mergeCell ref="A{referenceStartRow}:E{referenceStartRow}"/>{refDataRowMerges}</mergeCells>""");
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

    private static string ShortWeekday(DayOfWeek dow) => dow switch
    {
        DayOfWeek.Monday    => "T2",
        DayOfWeek.Tuesday   => "T3",
        DayOfWeek.Wednesday => "T4",
        DayOfWeek.Thursday  => "T5",
        DayOfWeek.Friday    => "T6",
        DayOfWeek.Saturday  => "T7",
        _                   => "CN",
    };

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

    // Fonts:
    //   0 default (11)  |  1 bold 11  |  2 bold 11 white  |  3 bold 20 dark-blue (title)  |  4 italic 10 gray (weekday)
    // Fills:
    //   0 none  |  1 gray125  |  2 EFEFEF (note label)  |  3 1F4E78 dark blue (header)  |  4 FFC7CE soft pink (weekend header)
    //   5 FCE4D6 light peach (weekend data)  |  6 DBEAF7 soft blue (section title)  |  7 FAFBFC very light gray (weekday label)
    // Borders:
    //   0 none  |  1 thin 9EADCC (all sides)
    // cellXfs indices used in code: 1=title  2=note label  3=header default  4=weekend header
    //   5=weekend data cell  6=weekday data cell  7=note value (wrap)  8=section title  9=reference table header
    //   10=weekday label default  11=weekday label weekend
    private static string BuildStyles() =>
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="5"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font><font><b/><sz val="20"/><color rgb="FF1F4E78"/><name val="Calibri"/></font><font><i/><sz val="10"/><color rgb="FF666666"/><name val="Calibri"/></font></fonts><fills count="8"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FFEFEFEF"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FF1F4E78"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFFFC7CE"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFFCE4D6"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFDBEAF7"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFFAFBFC"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="2"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style="thin"><color rgb="FF9EADCC"/></left><right style="thin"><color rgb="FF9EADCC"/></right><top style="thin"><color rgb="FF9EADCC"/></top><bottom style="thin"><color rgb="FF9EADCC"/></bottom><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="12"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="3" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf><xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf><xf numFmtId="0" fontId="2" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf><xf numFmtId="0" fontId="2" fillId="4" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf><xf numFmtId="0" fontId="0" fillId="5" borderId="1" xfId="0" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf><xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf><xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment vertical="center" wrapText="1"/></xf><xf numFmtId="0" fontId="1" fillId="6" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf><xf numFmtId="0" fontId="2" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf><xf numFmtId="0" fontId="4" fillId="7" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf><xf numFmtId="0" fontId="4" fillId="4" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""";
}
