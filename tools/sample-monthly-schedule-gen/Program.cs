// One-off generator cho file mẫu docs/sample-monthly-shift-import.xlsx.
// Layout 1 sheet tiếng Việt, có styling: title banner, header màu, weekend
// đánh dấu, OFF mờ, frozen panes, borders. Khớp MonthlyScheduleExcelParser.
//
// Chạy: dotnet run --project tools/sample-monthly-schedule-gen

using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

const int Year = 2026;
const int Month = 6;
var daysInMonth = DateTime.DaysInMonth(Year, Month);

// Layout rows (1-based, khớp Excel UI).
const uint RowTitle = 1;
const uint RowMetaName = 2;
const uint RowMetaMonth = 3;
const uint RowMetaNotes = 4;
const uint RowHeader = 6;          // Row 5 trống cho dễ nhìn.
const uint RowFirstData = 7;
const int ColFirstDay = 2;         // C (sau A=Mã NV, B=Họ tên)

var outputPath = ResolveOutputPath();
Console.WriteLine($"Writing to: {outputPath}");
if (File.Exists(outputPath)) File.Delete(outputPath);

using (var doc = SpreadsheetDocument.Create(outputPath, SpreadsheetDocumentType.Workbook))
{
    var workbookPart = doc.AddWorkbookPart();
    workbookPart.Workbook = new Workbook();

    // Stylesheet trước — cell xfs index sẽ dùng ở dưới.
    var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
    stylesPart.Stylesheet = BuildStylesheet();
    stylesPart.Stylesheet.Save();

    var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
    var worksheet = new Worksheet();

    // Cols: chiều rộng cột.
    var columns = new Columns();
    columns.Append(new Column { Min = 1, Max = 1, Width = 12, CustomWidth = true });   // A: Mã NV
    columns.Append(new Column { Min = 2, Max = 2, Width = 26, CustomWidth = true });   // B: Họ tên
    columns.Append(new Column
    {
        Min = (uint)(ColFirstDay + 1),
        Max = (uint)(ColFirstDay + daysInMonth),
        Width = 6,
        CustomWidth = true,
    });
    worksheet.Append(columns);

    // SheetView: freeze 2 cols + 6 rows (title + metadata + blank + header).
    var sheetViews = new SheetViews(new SheetView
    {
        WorkbookViewId = 0,
        TabSelected = true,
        Pane = new Pane
        {
            HorizontalSplit = 2D,
            VerticalSplit = (double)RowHeader,
            TopLeftCell = $"{ColumnLetter(ColFirstDay)}{RowFirstData}",
            ActivePane = PaneValues.BottomRight,
            State = PaneStateValues.Frozen,
        },
    });
    // SheetViews phải đặt trước Columns trong schema. Worksheet.Append vẫn OK
    // vì OpenXml reorder khi save — nhưng để chắc, prepend.
    worksheet.InsertAt(sheetViews, 0);

    var sheetData = new SheetData();
    worksheet.Append(sheetData);

    // ── Row 1: title banner ──────────────────────────────────────────────
    sheetData.AppendChild(BuildRow(RowTitle, 30,
        Cell("A1", StyleIndex.Title, $"BẢNG PHÂN CA THÁNG {Month:D2}/{Year}")));

    // ── Row 2-4: metadata ────────────────────────────────────────────────
    sheetData.AppendChild(BuildRow(RowMetaName, 20,
        Cell("A2", StyleIndex.MetaKey, "Tên bảng phân ca"),
        Cell("B2", StyleIndex.MetaValue, $"Bảng phân ca tháng {Month:D2}/{Year}")));
    sheetData.AppendChild(BuildRow(RowMetaMonth, 20,
        Cell("A3", StyleIndex.MetaKey, "Tháng"),
        Cell("B3", StyleIndex.MetaValue, $"{Month:D2}/{Year}")));
    sheetData.AppendChild(BuildRow(RowMetaNotes, 36,
        Cell("A4", StyleIndex.MetaKey, "Ghi chú"),
        Cell("B4", StyleIndex.MetaValueWrap,
            "Thay mã NV và mã ca trong các ô bên dưới bằng dữ liệu thực tế trước khi import. "
            + "Ô để trống hoặc ghi \"OFF\" / \"NGHỈ\" được hiểu là ngày không phân ca.")));

    // ── Row 6: header ────────────────────────────────────────────────────
    var headerCells = new List<Cell>
    {
        Cell("A6", StyleIndex.Header, "Mã NV"),
        Cell("B6", StyleIndex.Header, "Họ tên"),
    };
    for (var d = 1; d <= daysInMonth; d++)
    {
        var col = ColumnLetter(ColFirstDay + d - 1);
        var isWeekend = IsWeekend(Year, Month, d);
        headerCells.Add(Cell($"{col}6",
            isWeekend ? StyleIndex.HeaderWeekend : StyleIndex.Header,
            d.ToString(CultureInfo.InvariantCulture)));
    }
    sheetData.AppendChild(BuildRow(RowHeader, 26, headerCells.ToArray()));

    // ── Data rows ────────────────────────────────────────────────────────
    AppendEmployeeRow(sheetData, RowFirstData,     "NV001", "Nguyễn Văn Nam", alternate: true);
    AppendEmployeeRow(sheetData, RowFirstData + 1, "NV002", "Trần Thị Lan",   alternate: false);
    AppendEmployeeRow(sheetData, RowFirstData + 2, "",      "",               alternate: false, blank: true);
    AppendEmployeeRow(sheetData, RowFirstData + 3, "",      "",               alternate: false, blank: true);
    AppendEmployeeRow(sheetData, RowFirstData + 4, "",      "",               alternate: false, blank: true);

    // ── MergeCells: title spans toàn bộ bảng ─────────────────────────────
    var lastCol = ColumnLetter(ColFirstDay + daysInMonth - 1);
    var mergeCells = new MergeCells();
    mergeCells.Append(new MergeCell { Reference = $"A1:{lastCol}1" });
    // Notes value cell B4 spans phần còn lại cho dễ đọc.
    mergeCells.Append(new MergeCell { Reference = $"B4:{lastCol}4" });
    worksheet.Append(mergeCells);

    worksheetPart.Worksheet = worksheet;

    var sheets = workbookPart.Workbook.AppendChild(new Sheets());
    sheets.Append(new Sheet
    {
        Id = workbookPart.GetIdOfPart(worksheetPart),
        SheetId = 1U,
        Name = "Bảng phân ca",
    });

    workbookPart.Workbook.Save();
}

Console.WriteLine($"Done. File size: {new FileInfo(outputPath).Length} bytes");
return 0;

// ───────────────────────────────────────────────────────────────────────────
// Row builders
// ───────────────────────────────────────────────────────────────────────────

void AppendEmployeeRow(SheetData sd, uint rowIndex, string code, string name, bool alternate, bool blank = false)
{
    var cells = new List<Cell>
    {
        Cell($"A{rowIndex}", StyleIndex.EmpCode, code),
        Cell($"B{rowIndex}", StyleIndex.EmpName, name),
    };

    for (var d = 1; d <= daysInMonth; d++)
    {
        var col = ColumnLetter(ColFirstDay + d - 1);
        var weekend = IsWeekend(Year, Month, d);

        string value;
        StyleIndex style;
        if (blank)
        {
            value = string.Empty;
            style = weekend ? StyleIndex.CellWeekendBlank : StyleIndex.CellWeekdayBlank;
        }
        else if (weekend && new DateTime(Year, Month, d, 0, 0, 0, DateTimeKind.Utc).DayOfWeek == DayOfWeek.Sunday)
        {
            value = "NGHỈ";
            style = StyleIndex.CellOff;
        }
        else
        {
            value = alternate
                ? (d % 2 == 0 ? "CA_CHIEU" : "CA_SANG")
                : "CA_CHIEU";
            style = weekend ? StyleIndex.CellWeekend : StyleIndex.CellWeekday;
        }

        cells.Add(Cell($"{col}{rowIndex}", style, value));
    }

    sd.AppendChild(BuildRow(rowIndex, 20, cells.ToArray()));
}

static Row BuildRow(uint rowIndex, double height, params Cell[] cells)
{
    var row = new Row
    {
        RowIndex = rowIndex,
        Height = height,
        CustomHeight = true,
    };
    foreach (var c in cells) row.AppendChild(c);
    return row;
}

static Cell Cell(string cellRef, StyleIndex styleIndex, string value)
{
    var cell = new Cell
    {
        CellReference = cellRef,
        StyleIndex = (uint)styleIndex,
    };
    if (!string.IsNullOrEmpty(value))
    {
        cell.DataType = CellValues.InlineString;
        cell.InlineString = new InlineString(new Text(value) { Space = SpaceProcessingModeValues.Preserve });
    }
    return cell;
}

static string ColumnLetter(int zeroBasedIndex)
{
    var n = zeroBasedIndex + 1;
    var s = string.Empty;
    while (n > 0)
    {
        n--;
        s = (char)('A' + (n % 26)) + s;
        n /= 26;
    }
    return s;
}

static bool IsWeekend(int year, int month, int day)
{
    var dow = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc).DayOfWeek;
    return dow is DayOfWeek.Saturday or DayOfWeek.Sunday;
}

static string ResolveOutputPath()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs")))
        dir = dir.Parent;
    if (dir is null)
        throw new InvalidOperationException("Không tìm thấy thư mục 'docs/' từ AppContext.BaseDirectory.");
    return Path.Combine(dir.FullName, "docs", "sample-monthly-shift-import.xlsx");
}

// ───────────────────────────────────────────────────────────────────────────
// Stylesheet
// ───────────────────────────────────────────────────────────────────────────

static Stylesheet BuildStylesheet()
{
    // Fonts
    var fonts = new Fonts(
        new Font(new FontSize { Val = 11 }, new FontName { Val = "Calibri" }),                                       // 0 default
        new Font(new Bold(), new FontSize { Val = 11 }, new Color { Rgb = "FFFFFFFF" }, new FontName { Val = "Calibri" }), // 1 header white bold
        new Font(new Bold(), new FontSize { Val = 11 }, new FontName { Val = "Calibri" }),                            // 2 bold
        new Font(new Bold(), new FontSize { Val = 16 }, new Color { Rgb = "FF1F4E78" }, new FontName { Val = "Calibri" }), // 3 title big blue
        new Font(new Italic(), new FontSize { Val = 10 }, new Color { Rgb = "FF888888" }, new FontName { Val = "Calibri" }) // 4 OFF italic gray
    ) { Count = 5 };

    // Fills (index 0 None + 1 Gray125 bắt buộc).
    var fills = new Fills(
        new Fill(new PatternFill { PatternType = PatternValues.None }),
        new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
        new Fill(new PatternFill                                            // 2 header dark blue
        {
            PatternType = PatternValues.Solid,
            ForegroundColor = new ForegroundColor { Rgb = "FF1F4E78" },
            BackgroundColor = new BackgroundColor { Indexed = 64 },
        }),
        new Fill(new PatternFill                                            // 3 meta key light gray
        {
            PatternType = PatternValues.Solid,
            ForegroundColor = new ForegroundColor { Rgb = "FFF2F2F2" },
            BackgroundColor = new BackgroundColor { Indexed = 64 },
        }),
        new Fill(new PatternFill                                            // 4 weekend cell light yellow
        {
            PatternType = PatternValues.Solid,
            ForegroundColor = new ForegroundColor { Rgb = "FFFFF4D6" },
            BackgroundColor = new BackgroundColor { Indexed = 64 },
        }),
        new Fill(new PatternFill                                            // 5 weekend header soft red
        {
            PatternType = PatternValues.Solid,
            ForegroundColor = new ForegroundColor { Rgb = "FFC0504D" },
            BackgroundColor = new BackgroundColor { Indexed = 64 },
        }),
        new Fill(new PatternFill                                            // 6 OFF cell muted gray
        {
            PatternType = PatternValues.Solid,
            ForegroundColor = new ForegroundColor { Rgb = "FFEDEDED" },
            BackgroundColor = new BackgroundColor { Indexed = 64 },
        })
    ) { Count = 7 };

    // Borders
    var borders = new Borders(
        new Border(                                                          // 0 default empty
            new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder()),
        new Border(                                                          // 1 thin all sides
            new LeftBorder(new Color { Rgb = "FFBFBFBF" }) { Style = BorderStyleValues.Thin },
            new RightBorder(new Color { Rgb = "FFBFBFBF" }) { Style = BorderStyleValues.Thin },
            new TopBorder(new Color { Rgb = "FFBFBFBF" }) { Style = BorderStyleValues.Thin },
            new BottomBorder(new Color { Rgb = "FFBFBFBF" }) { Style = BorderStyleValues.Thin },
            new DiagonalBorder())
    ) { Count = 2 };

    var cellStyleXfs = new CellStyleFormats(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 })
    { Count = 1 };

    // CellXfs (thứ tự MUST khớp enum StyleIndex).
    var cellXfs = new CellFormats(
        // 0 Default
        new CellFormat { FontId = 0, FillId = 0, BorderId = 0, FormatId = 0 },
        // 1 Title — center, bold, large blue
        new CellFormat
        {
            FontId = 3, FillId = 0, BorderId = 0, FormatId = 0, ApplyAlignment = true,
            Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Center, Vertical = VerticalAlignmentValues.Center },
        },
        // 2 MetaKey — bold, light gray bg, left, border
        new CellFormat
        {
            FontId = 2, FillId = 3, BorderId = 1, FormatId = 0,
            ApplyFont = true, ApplyFill = true, ApplyBorder = true, ApplyAlignment = true,
            Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Left, Vertical = VerticalAlignmentValues.Center, Indent = 1 },
        },
        // 3 MetaValue
        new CellFormat
        {
            FontId = 0, FillId = 0, BorderId = 1, FormatId = 0,
            ApplyBorder = true, ApplyAlignment = true,
            Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Left, Vertical = VerticalAlignmentValues.Center, Indent = 1 },
        },
        // 4 MetaValueWrap (cho Notes)
        new CellFormat
        {
            FontId = 0, FillId = 0, BorderId = 1, FormatId = 0,
            ApplyBorder = true, ApplyAlignment = true,
            Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Left, Vertical = VerticalAlignmentValues.Center, WrapText = true, Indent = 1 },
        },
        // 5 Header weekday
        new CellFormat
        {
            FontId = 1, FillId = 2, BorderId = 1, FormatId = 0,
            ApplyFont = true, ApplyFill = true, ApplyBorder = true, ApplyAlignment = true,
            Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Center, Vertical = VerticalAlignmentValues.Center },
        },
        // 6 Header weekend
        new CellFormat
        {
            FontId = 1, FillId = 5, BorderId = 1, FormatId = 0,
            ApplyFont = true, ApplyFill = true, ApplyBorder = true, ApplyAlignment = true,
            Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Center, Vertical = VerticalAlignmentValues.Center },
        },
        // 7 EmpCode — bold left
        new CellFormat
        {
            FontId = 2, FillId = 0, BorderId = 1, FormatId = 0,
            ApplyFont = true, ApplyBorder = true, ApplyAlignment = true,
            Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Center, Vertical = VerticalAlignmentValues.Center },
        },
        // 8 EmpName — left
        new CellFormat
        {
            FontId = 0, FillId = 0, BorderId = 1, FormatId = 0,
            ApplyBorder = true, ApplyAlignment = true,
            Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Left, Vertical = VerticalAlignmentValues.Center, Indent = 1 },
        },
        // 9 CellWeekday — center, border
        new CellFormat
        {
            FontId = 0, FillId = 0, BorderId = 1, FormatId = 0,
            ApplyBorder = true, ApplyAlignment = true,
            Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Center, Vertical = VerticalAlignmentValues.Center },
        },
        // 10 CellWeekend — center, yellow bg
        new CellFormat
        {
            FontId = 0, FillId = 4, BorderId = 1, FormatId = 0,
            ApplyFill = true, ApplyBorder = true, ApplyAlignment = true,
            Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Center, Vertical = VerticalAlignmentValues.Center },
        },
        // 11 CellOff — italic gray
        new CellFormat
        {
            FontId = 4, FillId = 6, BorderId = 1, FormatId = 0,
            ApplyFont = true, ApplyFill = true, ApplyBorder = true, ApplyAlignment = true,
            Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Center, Vertical = VerticalAlignmentValues.Center },
        },
        // 12 CellWeekdayBlank — border only (cho hàng trống user tự nhập)
        new CellFormat
        {
            FontId = 0, FillId = 0, BorderId = 1, FormatId = 0,
            ApplyBorder = true,
        },
        // 13 CellWeekendBlank — yellow + border
        new CellFormat
        {
            FontId = 0, FillId = 4, BorderId = 1, FormatId = 0,
            ApplyFill = true, ApplyBorder = true,
        }
    ) { Count = 14 };

    var cellStyles = new CellStyles(new CellStyle { Name = "Normal", FormatId = 0U, BuiltinId = 0U }) { Count = 1 };

    return new Stylesheet(fonts, fills, borders, cellStyleXfs, cellXfs, cellStyles);
}

enum StyleIndex : uint
{
    Default = 0,
    Title = 1,
    MetaKey = 2,
    MetaValue = 3,
    MetaValueWrap = 4,
    Header = 5,
    HeaderWeekend = 6,
    EmpCode = 7,
    EmpName = 8,
    CellWeekday = 9,
    CellWeekend = 10,
    CellOff = 11,
    CellWeekdayBlank = 12,
    CellWeekendBlank = 13,
}
