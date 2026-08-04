using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using MPS.Application.Common.Interfaces.Attendance;

namespace MPS.Infrastructure.Services.AttendanceSheet;

/// <summary>
/// Đọc file .xlsx sinh bởi <see cref="AttendanceSheetExcelExporter"/>.
/// Layout dự kiến: row 4 title, rows 6-7 header (row 6 số ngày, row 7 thứ), rows 8+ body.
/// Body: mỗi NV chiếm N rows liên tiếp — merged cells cột A/B/C/D chỉ có value ở top row.
/// Cột E = shift code (hoặc "TC" / "(Chưa phân ca)"), cột F..N = ký hiệu ngày.
///
/// Detect NV block: TT ở cột A (row đầu block có value non-empty). Walk down đến TT tiếp theo.
/// </summary>
public sealed class AttendanceSheetExcelParser : IAttendanceSheetExcelParser
{
    private const int HeaderRow = 6;
    private const int BodyStartRow = 8;
    private const int FirstDayColumn = 6; // col F

    public AttendanceSheetParseResult Parse(Stream stream)
    {
        var errors = new List<AttendanceSheetParseError>();
        var employees = new List<AttendanceSheetParsedEmployeeBlock>();

        if (stream.CanSeek) stream.Position = 0;

        SpreadsheetDocument doc;
        try
        {
            doc = SpreadsheetDocument.Open(stream, isEditable: false);
        }
        catch (Exception ex)
        {
            errors.Add(new AttendanceSheetParseError(null, null, "INVALID_XLSX",
                $"Không đọc được file: {ex.Message}"));
            return new AttendanceSheetParseResult(0, employees, errors);
        }

        using (doc)
        {
            var wbPart = doc.WorkbookPart;
            var sheet = wbPart?.Workbook.Descendants<Sheet>().FirstOrDefault();
            if (wbPart is null || sheet?.Id?.Value is null)
            {
                errors.Add(new AttendanceSheetParseError(null, null, "EMPTY_WORKBOOK", "File không có sheet nào."));
                return new AttendanceSheetParseResult(0, employees, errors);
            }

            var sheetPart = (WorksheetPart)wbPart.GetPartById(sheet.Id!.Value!);
            var sharedStrings = wbPart.SharedStringTablePart?.SharedStringTable;

            // Build row map indexed by row number → cells indexed by column
            var rowMap = new Dictionary<int, Dictionary<int, string>>();
            foreach (var row in sheetPart.Worksheet.Descendants<Row>())
            {
                if (row.RowIndex?.Value is not uint rowIdx) continue;
                var cellMap = new Dictionary<int, string>();
                foreach (var cell in row.Elements<Cell>())
                {
                    if (cell.CellReference?.Value is not string cellRef) continue;
                    var (rIdx, cIdx) = ParseRef(cellRef);
                    if (rIdx != rowIdx) continue;
                    var value = ReadCellValue(cell, sharedStrings);
                    if (!string.IsNullOrEmpty(value)) cellMap[cIdx] = value;
                }
                rowMap[(int)rowIdx] = cellMap;
            }

            // Determine total days from header row 6: count consecutive numeric day headers from col F.
            var totalDays = 0;
            if (rowMap.TryGetValue(HeaderRow, out var headerCells))
            {
                for (var col = FirstDayColumn; ; col++)
                {
                    if (!headerCells.TryGetValue(col, out var v)) break;
                    if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) break;
                    totalDays++;
                }
            }
            if (totalDays == 0)
            {
                errors.Add(new AttendanceSheetParseError(HeaderRow, FirstDayColumn, "MISSING_DAY_HEADERS",
                    "Không tìm thấy cột ngày ở header row 6."));
                return new AttendanceSheetParseResult(0, employees, errors);
            }

            // Iterate body: detect NV blocks. NV boundary = row có TT ở cột A.
            var currentBlock = new List<(int Row, string ShiftLabel, Dictionary<int, string> Cells)>();
            string? currentEmpCode = null;
            string? currentEmpName = null;
            string? currentPosition = null;
            var currentTopRow = 0;

            var maxRow = rowMap.Keys.DefaultIfEmpty(0).Max();
            for (var r = BodyStartRow; r <= maxRow; r++)
            {
                if (!rowMap.TryGetValue(r, out var cells)) continue;

                var ttValue = cells.GetValueOrDefault(1); // col A
                var isNewBlock = !string.IsNullOrWhiteSpace(ttValue);

                if (isNewBlock)
                {
                    // Flush previous
                    if (!string.IsNullOrEmpty(currentEmpCode))
                    {
                        employees.Add(BuildBlock(currentTopRow, currentEmpCode, currentEmpName, currentPosition, currentBlock));
                    }
                    currentBlock = new List<(int, string, Dictionary<int, string>)>();
                    currentEmpCode = cells.GetValueOrDefault(2)?.Trim() ?? string.Empty;
                    currentEmpName = cells.GetValueOrDefault(3)?.Trim() ?? string.Empty;
                    currentPosition = cells.GetValueOrDefault(4)?.Trim() ?? string.Empty;
                    currentTopRow = r;

                    // Nếu block mới không có Mã NV (VD row "Chú thích ký hiệu:" của legend) → stop.
                    // Legend luôn ở cuối sheet, không có TT hợp lệ + col B (Mã NV) rỗng.
                    if (string.IsNullOrEmpty(currentEmpCode))
                    {
                        break;
                    }
                }

                var shiftLabel = cells.GetValueOrDefault(5)?.Trim();
                if (string.IsNullOrEmpty(shiftLabel))
                {
                    // Row không có Ca → skip (có thể là footer/blank row)
                    continue;
                }
                if (currentEmpCode is null)
                {
                    // Row có Ca mà chưa gặp NV → skip
                    continue;
                }
                currentBlock.Add((r, shiftLabel, cells));
            }
            // Flush cuối cùng
            if (!string.IsNullOrEmpty(currentEmpCode))
            {
                employees.Add(BuildBlock(currentTopRow, currentEmpCode, currentEmpName, currentPosition, currentBlock));
            }

            return new AttendanceSheetParseResult(totalDays, employees, errors);
        }
    }

    private static AttendanceSheetParsedEmployeeBlock BuildBlock(
        int topRow,
        string? empCode,
        string? empName,
        string? position,
        IReadOnlyList<(int Row, string ShiftLabel, Dictionary<int, string> Cells)> rows)
    {
        var shiftRows = new List<AttendanceSheetParsedShiftRow>(rows.Count);
        foreach (var (row, shiftLabel, cells) in rows)
        {
            var symbolByDay = new Dictionary<int, string>();
            foreach (var kv in cells)
            {
                var col = kv.Key;
                if (col < FirstDayColumn) continue;
                var dayIdx = col - FirstDayColumn; // 0-based
                var value = kv.Value?.Trim();
                if (!string.IsNullOrEmpty(value)) symbolByDay[dayIdx] = value;
            }
            shiftRows.Add(new AttendanceSheetParsedShiftRow(row, shiftLabel, symbolByDay));
        }
        return new AttendanceSheetParsedEmployeeBlock(
            topRow, empCode ?? string.Empty, empName ?? string.Empty, position ?? string.Empty, shiftRows);
    }

    // ─── OpenXML helpers ───────────────────────────────────────────────────────

    private static string ReadCellValue(Cell cell, SharedStringTable? sharedStrings)
    {
        if (cell.CellValue is null && cell.InlineString is null) return string.Empty;

        if (cell.DataType?.Value == CellValues.SharedString && cell.CellValue is not null)
        {
            if (int.TryParse(cell.CellValue.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx)
                && sharedStrings is not null && idx >= 0 && idx < sharedStrings.ChildElements.Count)
            {
                return sharedStrings.ElementAt(idx).InnerText;
            }
            return string.Empty;
        }
        if (cell.DataType?.Value == CellValues.InlineString && cell.InlineString is not null)
        {
            return cell.InlineString.InnerText;
        }
        if (cell.DataType?.Value == CellValues.Boolean && cell.CellValue is not null)
        {
            return cell.CellValue.Text == "1" ? "TRUE" : "FALSE";
        }
        return cell.CellValue?.Text ?? string.Empty;
    }

    private static (int Row, int Column) ParseRef(string cellRef)
    {
        var letters = new string(cellRef.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
        var digits = new string(cellRef.SkipWhile(char.IsLetter).ToArray());
        var col = letters.Aggregate(0, (acc, c) => acc * 26 + (c - 'A' + 1));
        return (int.Parse(digits, CultureInfo.InvariantCulture), col);
    }
}
