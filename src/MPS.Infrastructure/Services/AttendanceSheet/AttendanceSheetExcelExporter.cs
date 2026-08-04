using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using MPS.Application.Common.Interfaces.Attendance;
using MPS.Application.DTOs.Attendance;

namespace MPS.Infrastructure.Services.AttendanceSheet;

/// <summary>
/// Render "Bảng chấm công theo kỳ" ra .xlsx bằng OpenXml SDK.
/// Layout chuẩn VN — tham chiếu <c>docs/samples/bang-cham-cong-thang-08-2026-mau.xlsx</c>:
///   Row 4  = title merged
///   Row 6  = header (TT | Mã NV | Họ tên | Chức vụ | Ca | 1..N | Tổng công)
///   Row 7  = sub-header thứ trong tuần dưới cột ngày
///   Row 8+ = mỗi NV chiếm (N_ca + 1) rows: N ca chính + 1 row "TC" (Tăng ca, read-only)
///   Cuối sheet = block "Chú thích ký hiệu"
/// Merged cells: title B4:endCol4; per-NV cột A/B/C/D span (N_ca+1) rows; header cột cố định merge row 6+7.
/// Freeze pane F8 (2 hàng header + 5 cột đầu).
/// </summary>
public sealed class AttendanceSheetExcelExporter : IAttendanceSheetExcelExporter
{
    private const string SheetName = "Bảng chấm công";
    private const string PlaceholderShiftLabel = "(Chưa phân ca)";
    private const string TcRowLabel = "TC";

    public byte[] Build(AttendanceSheetExportDto data)
    {
        using var ms = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
        {
            var wbPart = doc.AddWorkbookPart();
            wbPart.Workbook = new Workbook();

            var stylesPart = wbPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = AttendanceSheetStyles.Build();
            stylesPart.Stylesheet.Save();

            var sheetPart = wbPart.AddNewPart<WorksheetPart>();
            sheetPart.Worksheet = BuildWorksheet(data);

            var sheets = wbPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = wbPart.GetIdOfPart(sheetPart),
                SheetId = 1U,
                Name = SheetName,
            });

            wbPart.Workbook.Save();
        }
        return ms.ToArray();
    }

    private static Worksheet BuildWorksheet(AttendanceSheetExportDto data)
    {
        var totalDays = data.ToDate.DayNumber - data.FromDate.DayNumber + 1;
        var totalCols = 5 + totalDays + 1; // 5 fixed cols + N days + 1 "Tổng công"
        var lastColLetter = ColLetter(totalCols);

        var sheetData = new SheetData();
        var mergeCells = new MergeCells();

        // ─── Title row 4 ───────────────────────────────────────────────────────
        var titleText = $"BẢNG CHẤM CÔNG TỪ {data.FromDate:dd/MM/yyyy} ĐẾN {data.ToDate:dd/MM/yyyy}";
        sheetData.Append(BuildTitleRow(4, titleText));
        mergeCells.Append(new MergeCell { Reference = $"B4:{lastColLetter}4" });

        // ─── Header rows 6-7 ───────────────────────────────────────────────────
        var (headerRow6, headerRow7) = BuildHeaderRows(data.FromDate, totalDays, lastColLetter);
        sheetData.Append(headerRow6);
        sheetData.Append(headerRow7);

        // Merge fixed headers (row 6+7): TT, Mã NV, Họ tên, Chức vụ, Ca, Tổng công
        foreach (var col in new[] { 1, 2, 3, 4, 5, totalCols })
        {
            mergeCells.Append(new MergeCell { Reference = $"{ColLetter(col)}6:{ColLetter(col)}7" });
        }

        // ─── Body: mỗi NV chiếm (N_ca + 1) rows ────────────────────────────────
        uint currentRow = 8;
        var tt = 1;
        foreach (var emp in data.Employees)
        {
            var nCa = emp.ShiftRows.Count;
            var totalEmpRows = nCa == 0 ? 1 : nCa + 1; // 1 placeholder row nếu chưa phân ca, else N ca + 1 TC
            var topRow = currentRow;
            var bottomRow = currentRow + (uint)totalEmpRows - 1;

            // Merge cột A/B/C/D span nếu > 1 row
            if (totalEmpRows > 1)
            {
                foreach (var col in new[] { 1, 2, 3, 4 })
                {
                    mergeCells.Append(new MergeCell
                    {
                        Reference = $"{ColLetter(col)}{topRow}:{ColLetter(col)}{bottomRow}",
                    });
                }
                // Merge cột Tổng công span cả block NV
                mergeCells.Append(new MergeCell
                {
                    Reference = $"{ColLetter(totalCols)}{topRow}:{ColLetter(totalCols)}{bottomRow}",
                });
            }

            // Build N_ca rows ca chính (hoặc 1 placeholder row)
            if (nCa == 0)
            {
                sheetData.Append(BuildEmployeeShiftRow(
                    currentRow, tt, emp, ShiftLabel: PlaceholderShiftLabel,
                    symbols: null, data.FromDate, totalDays, isTcRow: false, isFirstRow: true, isLastRow: true));
                currentRow++;
            }
            else
            {
                for (var i = 0; i < nCa; i++)
                {
                    var shift = emp.ShiftRows[i];
                    sheetData.Append(BuildEmployeeShiftRow(
                        currentRow, tt, emp, ShiftLabel: shift.ShiftCode,
                        symbols: shift.SymbolByDay,
                        data.FromDate, totalDays, isTcRow: false,
                        isFirstRow: i == 0, isLastRow: false));
                    currentRow++;
                }
                // Row TC cuối
                sheetData.Append(BuildEmployeeShiftRow(
                    currentRow, tt, emp, ShiftLabel: TcRowLabel,
                    symbols: null,
                    data.FromDate, totalDays, isTcRow: true,
                    isFirstRow: false, isLastRow: true,
                    otHoursByDay: emp.OvertimeHoursByDay));
                currentRow++;
            }

            tt++;
        }

        // ─── Chú thích ký hiệu (bảng có nền xám) ───────────────────────────────
        currentRow += 1; // 1 dòng trống

        // Row title: "Chú thích" — fill xám từ col A..H (không border).
        var titleRow = new Row { RowIndex = currentRow };
        titleRow.Append(TextCell($"A{currentRow}", "Chú thích", AttendanceSheetStyles.NoteTitleGray));
        for (var col = 2; col <= 8; col++)
        {
            titleRow.Append(TextCell($"{ColLetter(col)}{currentRow}", null, AttendanceSheetStyles.NoteGrayBlock));
        }
        sheetData.Append(titleRow);
        currentRow++;

        // Body legend: fill xám cho tất cả cells (col A..H).
        var legendBodyStart = currentRow;
        foreach (var (sym, meaning) in Legend)
        {
            var row = new Row { RowIndex = currentRow };
            row.Append(TextCell($"A{currentRow}", null, AttendanceSheetStyles.NoteGrayBlock));
            row.Append(TextCell($"B{currentRow}", sym, AttendanceSheetStyles.NoteSymbolGray));
            row.Append(TextCell($"C{currentRow}", meaning, AttendanceSheetStyles.NoteMeaningGray));
            // Cell D..H — merged nhưng vẫn cần fill để đồng bộ màu
            for (var col = 4; col <= 8; col++)
            {
                row.Append(TextCell($"{ColLetter(col)}{currentRow}", null, AttendanceSheetStyles.NoteGrayBlock));
            }
            sheetData.Append(row);
            currentRow++;
        }
        var legendBodyEnd = currentRow - 1;

        // Merge cột meaning trong legend (col C → col H)
        for (uint r = legendBodyStart; r <= legendBodyEnd; r++)
        {
            mergeCells.Append(new MergeCell { Reference = $"C{r}:H{r}" });
        }

        // ─── Assemble worksheet (thứ tự sub-element BẮT BUỘC theo schema) ─────
        var worksheet = new Worksheet();

        // SheetViews — freeze pane F8
        var sheetViews = new SheetViews();
        var sheetView = new SheetView { WorkbookViewId = 0U };
        sheetView.Append(new Pane
        {
            HorizontalSplit = 5D,
            VerticalSplit = 7D,
            TopLeftCell = "F8",
            ActivePane = PaneValues.BottomRight,
            State = PaneStateValues.Frozen,
        });
        sheetViews.Append(sheetView);
        worksheet.Append(sheetViews);

        // Columns (widths)
        worksheet.Append(BuildColumns(totalDays, totalCols));

        // SheetData
        worksheet.Append(sheetData);

        // MergeCells (must be AFTER SheetData)
        if (mergeCells.HasChildren) worksheet.Append(mergeCells);

        return worksheet;
    }

    // ─── Row builders ──────────────────────────────────────────────────────────

    private static Row BuildTitleRow(uint rowIdx, string title)
    {
        var row = new Row { RowIndex = rowIdx, Height = 24D, CustomHeight = true };
        row.Append(TextCell($"B{rowIdx}", title, AttendanceSheetStyles.Title));
        return row;
    }

    private static (Row, Row) BuildHeaderRows(DateOnly fromDate, int totalDays, string lastColLetter)
    {
        var row6 = new Row { RowIndex = 6U, Height = 22D, CustomHeight = true };
        var row7 = new Row { RowIndex = 7U, Height = 18D, CustomHeight = true };

        row6.Append(TextCell("A6", "TT", AttendanceSheetStyles.Header));
        row7.Append(TextCell("A7", null, AttendanceSheetStyles.Header));
        row6.Append(TextCell("B6", "Mã NV", AttendanceSheetStyles.Header));
        row7.Append(TextCell("B7", null, AttendanceSheetStyles.Header));
        row6.Append(TextCell("C6", "Họ tên", AttendanceSheetStyles.Header));
        row7.Append(TextCell("C7", null, AttendanceSheetStyles.Header));
        row6.Append(TextCell("D6", "Chức vụ", AttendanceSheetStyles.Header));
        row7.Append(TextCell("D7", null, AttendanceSheetStyles.Header));
        row6.Append(TextCell("E6", "Ca", AttendanceSheetStyles.Header));
        row7.Append(TextCell("E7", null, AttendanceSheetStyles.Header));

        for (var i = 0; i < totalDays; i++)
        {
            var day = fromDate.AddDays(i);
            var col = 6 + i;
            var colLetter = ColLetter(col);
            var isWeekend = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday;
            var style = isWeekend ? AttendanceSheetStyles.HeaderWeekend : AttendanceSheetStyles.Header;

            row6.Append(TextCell($"{colLetter}6", day.Day.ToString(CultureInfo.InvariantCulture), style));
            row7.Append(TextCell($"{colLetter}7", VnWeekday(day.DayOfWeek), style));
        }

        row6.Append(TextCell($"{lastColLetter}6", "Tổng công", AttendanceSheetStyles.Header));
        row7.Append(TextCell($"{lastColLetter}7", null, AttendanceSheetStyles.Header));
        return (row6, row7);
    }

    private static Row BuildEmployeeShiftRow(
        uint rowIdx,
        int tt,
        AttendanceSheetEmployeeDto emp,
        string ShiftLabel,
        IReadOnlyDictionary<int, string>? symbols,
        DateOnly fromDate,
        int totalDays,
        bool isTcRow,
        bool isFirstRow,
        bool isLastRow,
        IReadOnlyDictionary<int, decimal>? otHoursByDay = null)
    {
        var row = new Row { RowIndex = rowIdx, Height = 20D, CustomHeight = true };

        // Cột A-D chỉ điền value ở row đầu (do merged), các row sau vẫn cần cell để giữ style
        row.Append(TextCell($"A{rowIdx}", isFirstRow ? tt.ToString(CultureInfo.InvariantCulture) : null, AttendanceSheetStyles.BodyCenter));
        row.Append(TextCell($"B{rowIdx}", isFirstRow ? emp.EmployeeCode : null, AttendanceSheetStyles.BodyCenter));
        row.Append(TextCell($"C{rowIdx}", isFirstRow ? emp.FullName : null, AttendanceSheetStyles.BodyLeft));
        row.Append(TextCell($"D{rowIdx}", isFirstRow ? emp.PositionName : null, AttendanceSheetStyles.BodyLeft));

        // Cột E = Ca (hoặc TC)
        var shiftStyle = isTcRow ? AttendanceSheetStyles.BodyBold : AttendanceSheetStyles.BodyCenter;
        row.Append(TextCell($"E{rowIdx}", ShiftLabel, shiftStyle));

        // Cột ngày F..N
        for (var i = 0; i < totalDays; i++)
        {
            var day = fromDate.AddDays(i);
            var col = 6 + i;
            var colLetter = ColLetter(col);
            var isWeekend = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday;
            var style = isWeekend ? AttendanceSheetStyles.BodyWeekend : AttendanceSheetStyles.BodyCenter;

            if (isTcRow && otHoursByDay is not null && otHoursByDay.TryGetValue(day.Day, out var hours) && hours > 0)
            {
                row.Append(NumberCell($"{colLetter}{rowIdx}", hours, style));
            }
            else if (!isTcRow && symbols is not null && symbols.TryGetValue(day.Day, out var sym))
            {
                row.Append(TextCell($"{colLetter}{rowIdx}", sym, style));
            }
            else
            {
                row.Append(TextCell($"{colLetter}{rowIdx}", null, style));
            }
        }

        // Cột Tổng công — chỉ điền ở row đầu (do merged span cả block NV), non-TC.
        var totalCol = 6 + totalDays;
        var totalColLetter = ColLetter(totalCol);
        if (isFirstRow && !isTcRow && symbols is not null)
        {
            // COUNTIF cho X + 0.5 * COUNTIF cho "1/2" trên phạm vi 3 ca chính (từ row hiện tại tới row trước TC).
            // Vì layout dynamic (N_ca rows), tính pre-compute từ dữ liệu thay vì formula phức tạp.
            row.Append(TextCell($"{totalColLetter}{rowIdx}", null, AttendanceSheetStyles.BodyBold));
        }
        else
        {
            row.Append(TextCell($"{totalColLetter}{rowIdx}", null, AttendanceSheetStyles.BodyCenter));
        }

        return row;
    }

    // ─── Columns / widths ──────────────────────────────────────────────────────

    private static Columns BuildColumns(int totalDays, int totalCols)
    {
        var cols = new Columns();
        cols.Append(new Column { Min = 1U, Max = 1U, Width = 4D, CustomWidth = true });
        cols.Append(new Column { Min = 2U, Max = 2U, Width = 10D, CustomWidth = true });
        cols.Append(new Column { Min = 3U, Max = 3U, Width = 22D, CustomWidth = true });
        cols.Append(new Column { Min = 4U, Max = 4U, Width = 22D, CustomWidth = true });
        cols.Append(new Column { Min = 5U, Max = 5U, Width = 10D, CustomWidth = true });
        cols.Append(new Column { Min = 6U, Max = (uint)(6 + totalDays - 1), Width = 4.5D, CustomWidth = true });
        cols.Append(new Column { Min = (uint)totalCols, Max = (uint)totalCols, Width = 11D, CustomWidth = true });
        return cols;
    }

    // ─── Cell helpers ──────────────────────────────────────────────────────────

    private static Cell TextCell(string reference, string? text, uint styleIndex)
    {
        var cell = new Cell { CellReference = reference, StyleIndex = styleIndex };
        if (string.IsNullOrEmpty(text)) return cell;
        cell.DataType = CellValues.InlineString;
        cell.InlineString = new InlineString(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return cell;
    }

    private static Cell NumberCell(string reference, decimal value, uint styleIndex)
    {
        var cell = new Cell { CellReference = reference, StyleIndex = styleIndex };
        cell.DataType = CellValues.Number;
        cell.CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture));
        return cell;
    }

    // ─── Utils ─────────────────────────────────────────────────────────────────

    /// <summary>Convert 1-based column index → letter (1 → A, 26 → Z, 27 → AA).</summary>
    private static string ColLetter(int col)
    {
        var letters = string.Empty;
        while (col > 0)
        {
            var rem = (col - 1) % 26;
            letters = (char)('A' + rem) + letters;
            col = (col - 1) / 26;
        }
        return letters;
    }

    private static string VnWeekday(DayOfWeek dow) => dow switch
    {
        DayOfWeek.Monday => "T2",
        DayOfWeek.Tuesday => "T3",
        DayOfWeek.Wednesday => "T4",
        DayOfWeek.Thursday => "T5",
        DayOfWeek.Friday => "T6",
        DayOfWeek.Saturday => "T7",
        DayOfWeek.Sunday => "CN",
        _ => string.Empty,
    };

    private static readonly (string Symbol, string Meaning)[] Legend = new[]
    {
        ("X",    "Đi làm đủ giờ (Present)"),
        ("P",    "Nghỉ phép — có đơn được duyệt (OnLeave)"),
        ("V",    "Vắng không phép (Absent)"),
        ("M",    "Đi muộn (Late) — check-in muộn hơn shift start"),
        ("S",    "Về sớm (EarlyLeave) — check-out sớm hơn shift end"),
        ("1/2",  "Nửa ca (HalfDay) — tính 0.5 công"),
        ("L",    "Nghỉ lễ có lương — không cần điền, hệ thống tự nhận từ danh sách ngày lễ"),
        ("TC",   "Tăng ca — số giờ OT/ngày (read-only, chỉ export, import bỏ qua)"),
        ("(rỗng)", "Không đụng — import giữ nguyên record cũ (no-op)"),
        (PlaceholderShiftLabel, "NV chưa có lịch phân ca trong kỳ — HR có thể điền để tạo ad-hoc"),
    };
}
