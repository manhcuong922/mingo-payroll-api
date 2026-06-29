using System.Globalization;
using MiniExcelLibs;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;

namespace MPS.Infrastructure.Services;

/// <summary>
/// Parser cho file excel "bảng phân ca theo tháng" — dùng MiniExcel để tránh
/// first-chance exception spam của DocumentFormat.OpenXml khi chạy với debugger.
///
/// Layout chính (1 sheet duy nhất):
///   Row 1..n: metadata key/value (cột A = nhãn, cột B = giá trị). Nhãn nhận diện:
///     - "Tên bảng phân ca" (hoặc "Schedule Name")
///     - "Tháng" (hoặc "Month")        — định dạng MM/YYYY
///     - "Ghi chú" (hoặc "Notes")
///   Tiếp đó là 1 hàng header chứa cột A = "Mã NV" / "Mã nhân viên" / "Employee Code",
///   cột B = "Họ tên" / "Họ và tên" / "Employee Name", các cột sau = số ngày 1..N.
///   Dưới header là dữ liệu: mỗi hàng 1 nhân viên, cell = mã ca, hoặc "OFF" / "NGHỈ" /
///   rỗng để bỏ qua ngày đó.
///
/// Vẫn nhận format cũ (2 sheet, headers tiếng Anh) để các file đã phát hành không vỡ.
/// </summary>
public sealed class MonthlyScheduleExcelParser : IMonthlyScheduleExcelParser
{
    private const string LegacyAssignmentsSheet = "Assignments";
    private const string LegacyScheduleInfoSheet = "ScheduleInfo";

    private static readonly string[] EmployeeCodeHeaderAliases =
        ["Mã NV", "Mã nhân viên", "Employee Code"];
    private static readonly string[] EmployeeNameHeaderAliases =
        ["Họ tên", "Họ và tên", "Employee Name"];
    private static readonly string[] ScheduleNameKeyAliases =
        ["Tên bảng phân ca", "Tên", "Schedule Name"];
    private static readonly string[] MonthKeyAliases =
        ["Tháng", "Month"];
    private static readonly string[] NotesKeyAliases =
        ["Ghi chú", "Notes"];
    private static readonly string[] OffMarkers =
        ["OFF", "NGHỈ", "NGHI", "N", "-"];

    public MonthlyScheduleParseResult Parse(Stream stream)
    {
        var errors = new List<MonthlyScheduleParseError>();
        var assignments = new List<MonthlyScheduleParsedAssignment>();

        // Đọc 1 lần ra byte[] rồi mỗi MiniExcel call mở MemoryStream RIÊNG +
        // dispose deterministic. Lý do: nếu reuse 1 stream giữa nhiều MiniExcel
        // call, có rủi ro stream-state/finalizer chạy ngoài tầm kiểm soát (đặc
        // biệt khi debugger giữ object lâu hơn) gây process crash khó debug.
        byte[] fileBytes;
        try
        {
            using var ms = new MemoryStream();
            if (stream.CanSeek) stream.Position = 0;
            stream.CopyTo(ms);
            fileBytes = ms.ToArray();
        }
        catch (Exception ex)
        {
            errors.Add(new MonthlyScheduleParseError(null, null, "INVALID_XLSX",
                $"Không đọc được file ({ex.GetType().Name}: {ex.Message})."));
            return new MonthlyScheduleParseResult(null, null, null, null, assignments, errors);
        }

        List<string> sheetNames;
        try
        {
            using var sheetStream = new MemoryStream(fileBytes, writable: false);
            sheetNames = MiniExcel.GetSheetNames(sheetStream).ToList();
        }
        catch (Exception ex)
        {
            errors.Add(new MonthlyScheduleParseError(null, null, "INVALID_XLSX",
                $"File không phải xlsx hợp lệ ({ex.GetType().Name}: {ex.Message})."));
            return new MonthlyScheduleParseResult(null, null, null, null, assignments, errors);
        }

        if (sheetNames.Count == 0)
        {
            errors.Add(new MonthlyScheduleParseError(null, null, "NO_SHEET",
                "File excel không có worksheet nào."));
            return new MonthlyScheduleParseResult(null, null, null, null, assignments, errors);
        }

        var hasLegacyInfo = sheetNames.Any(s =>
            string.Equals(s, LegacyScheduleInfoSheet, StringComparison.OrdinalIgnoreCase));
        var hasLegacyAssignments = sheetNames.Any(s =>
            string.Equals(s, LegacyAssignmentsSheet, StringComparison.OrdinalIgnoreCase));

        if (hasLegacyInfo && hasLegacyAssignments)
        {
            return ParseLegacyTwoSheet(fileBytes, assignments, errors);
        }

        return ParseSingleSheet(fileBytes, sheetNames[0], assignments, errors);
    }

    private static MonthlyScheduleParseResult ParseSingleSheet(
        byte[] fileBytes,
        string sheetName,
        List<MonthlyScheduleParsedAssignment> assignments,
        List<MonthlyScheduleParseError> errors)
    {
        var rows = QueryRows(fileBytes, sheetName);

        string? scheduleName = null;
        int? year = null;
        int? month = null;
        string? notes = null;

        var headerRowIndex = -1;
        string? employeeCodeColumnLetter = null;
        string? employeeNameColumnLetter = null;
        var columnLetterToDay = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var firstText = GetCellString(row, "A");

            if (IsAnyAlias(firstText, EmployeeCodeHeaderAliases))
            {
                headerRowIndex = r;
                foreach (var (letter, raw) in row)
                {
                    var text = ToTrimmedString(raw);
                    if (string.IsNullOrEmpty(text)) continue;

                    if (IsAnyAlias(text, EmployeeCodeHeaderAliases)) employeeCodeColumnLetter = letter;
                    else if (IsAnyAlias(text, EmployeeNameHeaderAliases)) employeeNameColumnLetter = letter;
                    else if (TryParseDay(text, out var day)) columnLetterToDay[letter] = day;
                }
                break;
            }

            // Vẫn còn metadata — đọc key/value (cột A/B).
            var key = firstText;
            var value = GetCellString(row, "B");
            if (string.IsNullOrEmpty(key)) continue;

            if (IsAnyAlias(key, ScheduleNameKeyAliases))
            {
                scheduleName = value;
            }
            else if (IsAnyAlias(key, MonthKeyAliases))
            {
                if (TryParseMonthFromCell(row, "B", out var y, out var m))
                {
                    year = y;
                    month = m;
                }
                else
                {
                    errors.Add(new MonthlyScheduleParseError(r + 1, null, "INVALID_MONTH",
                        $"Trường Tháng '{value}' không đúng định dạng MM/YYYY."));
                }
            }
            else if (IsAnyAlias(key, NotesKeyAliases))
            {
                notes = value;
            }
        }

        if (scheduleName is null)
            errors.Add(new MonthlyScheduleParseError(null, null, "MISSING_SCHEDULE_NAME",
                "Thiếu dòng 'Tên bảng phân ca' ở đầu sheet."));
        if (year is null || month is null)
            errors.Add(new MonthlyScheduleParseError(null, null, "MISSING_MONTH",
                "Thiếu dòng 'Tháng' (định dạng MM/YYYY) ở đầu sheet."));

        if (headerRowIndex < 0)
        {
            errors.Add(new MonthlyScheduleParseError(null, null, "MISSING_HEADER_ROW",
                "Không tìm thấy hàng header (phải có cột 'Mã NV' / 'Mã nhân viên')."));
            return new MonthlyScheduleParseResult(scheduleName, year, month, notes, assignments, errors);
        }
        if (employeeCodeColumnLetter is null)
        {
            errors.Add(new MonthlyScheduleParseError(headerRowIndex + 1, null, "MISSING_EMPLOYEE_CODE_HEADER",
                "Header không có cột 'Mã NV'."));
            return new MonthlyScheduleParseResult(scheduleName, year, month, notes, assignments, errors);
        }
        if (columnLetterToDay.Count == 0)
        {
            errors.Add(new MonthlyScheduleParseError(headerRowIndex + 1, null, "MISSING_DAY_HEADERS",
                "Header không có cột ngày (1..N)."));
            return new MonthlyScheduleParseResult(scheduleName, year, month, notes, assignments, errors);
        }

        ReadAssignmentRows(rows, headerRowIndex, year, month,
            employeeCodeColumnLetter, employeeNameColumnLetter, columnLetterToDay, assignments, errors);

        return new MonthlyScheduleParseResult(scheduleName, year, month, notes, assignments, errors);
    }

    // ─── Legacy 2-sheet parser (giữ để không phá file đã phát hành) ──────────────

    private static MonthlyScheduleParseResult ParseLegacyTwoSheet(
        byte[] fileBytes,
        List<MonthlyScheduleParsedAssignment> assignments,
        List<MonthlyScheduleParseError> errors)
    {
        string? scheduleName = null;
        int? year = null;
        int? month = null;
        string? notes = null;

        var infoRows = QueryRows(fileBytes, LegacyScheduleInfoSheet);
        foreach (var row in infoRows)
        {
            var key = GetCellString(row, "A");
            var value = GetCellString(row, "B");
            if (string.IsNullOrEmpty(key)) continue;

            if (IsAnyAlias(key, ScheduleNameKeyAliases))
                scheduleName = value;
            else if (IsAnyAlias(key, MonthKeyAliases))
            {
                if (TryParseMonthFromCell(row, "B", out var y, out var m))
                {
                    year = y;
                    month = m;
                }
                else
                {
                    errors.Add(new MonthlyScheduleParseError(null, null, "INVALID_MONTH",
                        $"Trường Month '{value}' không đúng định dạng MM/YYYY."));
                }
            }
            else if (IsAnyAlias(key, NotesKeyAliases))
                notes = value;
        }

        if (scheduleName is null)
            errors.Add(new MonthlyScheduleParseError(null, null, "MISSING_SCHEDULE_NAME",
                "Sheet ScheduleInfo thiếu trường 'Schedule Name'."));
        if (year is null || month is null)
            errors.Add(new MonthlyScheduleParseError(null, null, "MISSING_MONTH",
                "Sheet ScheduleInfo thiếu trường 'Month' (MM/YYYY)."));

        var assignmentRows = QueryRows(fileBytes, LegacyAssignmentsSheet);
        if (assignmentRows.Count == 0)
        {
            errors.Add(new MonthlyScheduleParseError(null, null, "EMPTY_ASSIGNMENTS",
                "Sheet Assignments rỗng."));
            return new MonthlyScheduleParseResult(scheduleName, year, month, notes, assignments, errors);
        }

        var headerRow = assignmentRows[0];
        string? employeeCodeColumnLetter = null;
        string? employeeNameColumnLetter = null;
        var columnLetterToDay = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (letter, raw) in headerRow)
        {
            var text = ToTrimmedString(raw);
            if (string.IsNullOrEmpty(text)) continue;

            if (IsAnyAlias(text, EmployeeCodeHeaderAliases)) employeeCodeColumnLetter = letter;
            else if (IsAnyAlias(text, EmployeeNameHeaderAliases)) employeeNameColumnLetter = letter;
            else if (TryParseDay(text, out var day)) columnLetterToDay[letter] = day;
        }
        if (employeeCodeColumnLetter is null)
        {
            errors.Add(new MonthlyScheduleParseError(1, null, "MISSING_EMPLOYEE_CODE_HEADER",
                "Sheet Assignments thiếu cột 'Employee Code'."));
            return new MonthlyScheduleParseResult(scheduleName, year, month, notes, assignments, errors);
        }
        if (columnLetterToDay.Count == 0)
        {
            errors.Add(new MonthlyScheduleParseError(1, null, "MISSING_DAY_HEADERS",
                "Sheet Assignments thiếu cột ngày (1..N)."));
            return new MonthlyScheduleParseResult(scheduleName, year, month, notes, assignments, errors);
        }

        ReadAssignmentRows(assignmentRows, headerRowIndex: 0, year, month,
            employeeCodeColumnLetter, employeeNameColumnLetter, columnLetterToDay, assignments, errors);

        return new MonthlyScheduleParseResult(scheduleName, year, month, notes, assignments, errors);
    }

    // ─── Shared row reader ──────────────────────────────────────────────────────

    private static void ReadAssignmentRows(
        IReadOnlyList<IDictionary<string, object>> rows,
        int headerRowIndex,
        int? year,
        int? month,
        string employeeCodeColumnLetter,
        string? employeeNameColumnLetter,
        IReadOnlyDictionary<string, int> columnLetterToDay,
        List<MonthlyScheduleParsedAssignment> assignments,
        List<MonthlyScheduleParseError> errors)
    {
        var daysInMonth = year.HasValue && month.HasValue
            ? DateTime.DaysInMonth(year.Value, month.Value)
            : (int?)null;

        for (var r = headerRowIndex + 1; r < rows.Count; r++)
        {
            var row = rows[r];
            var rowIndex = r + 1;

            var employeeCode = GetCellString(row, employeeCodeColumnLetter);
            if (string.IsNullOrEmpty(employeeCode))
            {
                continue;
            }

            string? employeeName = null;
            if (employeeNameColumnLetter is not null)
                employeeName = GetCellString(row, employeeNameColumnLetter);

            foreach (var (letter, day) in columnLetterToDay)
            {
                var raw = GetCellString(row, letter);
                if (string.IsNullOrEmpty(raw)) continue;
                if (IsOffMarker(raw)) continue;

                var columnIndex = ColumnLetterToIndex(letter);

                if (daysInMonth is { } dim && day > dim)
                {
                    errors.Add(new MonthlyScheduleParseError(rowIndex, columnIndex, "DAY_OUT_OF_MONTH",
                        $"Ngày {day} không tồn tại trong tháng {month:D2}/{year}."));
                    continue;
                }
                if (year is null || month is null)
                {
                    errors.Add(new MonthlyScheduleParseError(rowIndex, columnIndex, "MISSING_MONTH_FOR_CELL",
                        "Không xác định được tháng để build WorkDate."));
                    continue;
                }

                var workDate = new DateOnly(year.Value, month.Value, day);
                assignments.Add(new MonthlyScheduleParsedAssignment(
                    rowIndex, columnIndex, employeeCode, employeeName, workDate, raw));
            }
        }
    }

    // ─── MiniExcel helpers ──────────────────────────────────────────────────────

    private static List<IDictionary<string, object>> QueryRows(byte[] fileBytes, string sheetName)
    {
        // MemoryStream riêng + using → MiniExcel chỉ "thấy" stream này trong scope
        // của method, không có cách nào giữ reference sống lâu hơn (race với GC).
        using var stream = new MemoryStream(fileBytes, writable: false);
        return MiniExcel.Query(stream, useHeaderRow: false, sheetName: sheetName)
            .Cast<IDictionary<string, object>>()
            .ToList();
    }

    private static string? GetCellString(IDictionary<string, object> row, string columnLetter) =>
        row.TryGetValue(columnLetter, out var value) ? ToTrimmedString(value) : null;

    private static string? ToTrimmedString(object? value)
    {
        if (value is null) return null;
        if (value is string s) return s.Trim();
        if (value is DateTime dt) return dt.ToString("MM/yyyy", CultureInfo.InvariantCulture);
        if (value is double d)
        {
            return d == Math.Floor(d) && d is >= long.MinValue and <= long.MaxValue
                ? ((long)d).ToString(CultureInfo.InvariantCulture)
                : d.ToString(CultureInfo.InvariantCulture);
        }
        return value.ToString()?.Trim();
    }

    private static bool TryParseMonthFromCell(IDictionary<string, object> row, string columnLetter,
        out int year, out int month)
    {
        year = 0; month = 0;
        if (!row.TryGetValue(columnLetter, out var value) || value is null) return false;

        // Excel auto-formats "06/2026" thành DateTime — handle trực tiếp.
        if (value is DateTime dt)
        {
            year = dt.Year;
            month = dt.Month;
            return true;
        }

        var text = value is string s ? s.Trim() : value.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (DateTime.TryParseExact(text, new[] { "MM/yyyy", "M/yyyy" },
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            year = parsed.Year;
            month = parsed.Month;
            return true;
        }
        return false;
    }

    private static bool TryParseDay(string text, out int day)
    {
        day = 0;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d))
            return false;
        if (d is < 1 or > 31) return false;
        day = d;
        return true;
    }

    private static bool IsAnyAlias(string? value, IReadOnlyList<string> aliases) =>
        !string.IsNullOrEmpty(value)
        && aliases.Any(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase));

    private static bool IsOffMarker(string value) =>
        OffMarkers.Any(m => string.Equals(m, value, StringComparison.OrdinalIgnoreCase));

    private static int ColumnLetterToIndex(string column)
    {
        if (string.IsNullOrEmpty(column)) return -1;
        var result = 0;
        foreach (var c in column)
        {
            if (!char.IsLetter(c)) return -1;
            result = result * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
        }
        return result - 1;
    }
}
