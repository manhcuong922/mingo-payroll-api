using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;

namespace MPS.Infrastructure.Services;

/// <summary>
/// Parser cho file excel "bảng phân ca theo tháng".
/// Đọc trực tiếp cấu trúc xlsx (zip/xml) để tránh crash runtime từ các thư viện Excel
/// trong môi trường debug/local.
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
    private sealed record SheetRef(string Name, string RelationshipId, string Path);

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
    private static readonly Regex MonthInTitleRegex =
        new(@"(?<month>\d{1,2})\s*/\s*(?<year>\d{4})", RegexOptions.Compiled);
    private static readonly XNamespace SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    public MonthlyScheduleParseResult Parse(Stream stream)
    {
        var errors = new List<MonthlyScheduleParseError>();
        var assignments = new List<MonthlyScheduleParsedAssignment>();

        // Đọc 1 lần ra byte[] rồi các bước parse mở MemoryStream riêng.
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

        IReadOnlyList<SheetRef> sheets;
        try
        {
            sheets = ReadWorkbookSheets(fileBytes);
        }
        catch (Exception ex)
        {
            errors.Add(new MonthlyScheduleParseError(null, null, "INVALID_XLSX",
                $"File không phải xlsx hợp lệ ({ex.GetType().Name}: {ex.Message})."));
            return new MonthlyScheduleParseResult(null, null, null, null, assignments, errors);
        }

        if (sheets.Count == 0)
        {
            errors.Add(new MonthlyScheduleParseError(null, null, "NO_SHEET",
                "File excel không có worksheet nào."));
            return new MonthlyScheduleParseResult(null, null, null, null, assignments, errors);
        }

        try
        {
            var hasLegacyInfo = sheets.Any(s =>
                string.Equals(s.Name, LegacyScheduleInfoSheet, StringComparison.OrdinalIgnoreCase));
            var hasLegacyAssignments = sheets.Any(s =>
                string.Equals(s.Name, LegacyAssignmentsSheet, StringComparison.OrdinalIgnoreCase));

            if (hasLegacyInfo && hasLegacyAssignments)
                return ParseLegacyTwoSheet(fileBytes, assignments, errors);

            return ParseSingleSheet(fileBytes, sheets[0].Name, assignments, errors);
        }
        catch (Exception ex)
        {
            errors.Add(new MonthlyScheduleParseError(null, null, "INVALID_XLSX",
                $"Không đọc được nội dung file xlsx ({ex.GetType().Name}: {ex.Message})."));
            return new MonthlyScheduleParseResult(null, null, null, null, assignments, errors);
        }
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

            if (TryParseScheduleTitle(key, out var titleYear, out var titleMonth))
            {
                scheduleName ??= $"Bảng phân ca tháng {titleMonth:00}/{titleYear}";
                year ??= titleYear;
                month ??= titleMonth;
            }
            else if (IsAnyAlias(key, ScheduleNameKeyAliases))
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
                "Thiếu tiêu đề hoặc dòng 'Tên bảng phân ca' ở đầu sheet."));
        if (year is null || month is null)
            errors.Add(new MonthlyScheduleParseError(null, null, "MISSING_MONTH",
                "Thiếu tiêu đề tháng hoặc dòng 'Tháng' (định dạng MM/YYYY) ở đầu sheet."));

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

    // ─── Lightweight xlsx helpers ──────────────────────────────────────────────

    private static List<IDictionary<string, object>> QueryRows(byte[] fileBytes, string sheetName)
    {
        using var stream = new MemoryStream(fileBytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        var sheet = ReadWorkbookSheets(archive)
            .FirstOrDefault(s => string.Equals(s.Name, sheetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"Worksheet '{sheetName}' not found.");

        var worksheet = LoadXmlEntry(archive, sheet.Path)
            ?? throw new InvalidDataException($"Worksheet entry '{sheet.Path}' not found.");
        var sharedStrings = ReadSharedStrings(archive);
        var sheetData = worksheet.Root?.Element(SpreadsheetNs + "sheetData");
        if (sheetData is null) return [];

        var rows = new List<IDictionary<string, object>>();
        var fallbackRowNumber = 1;
        foreach (var rowElement in sheetData.Elements(SpreadsheetNs + "row"))
        {
            var rowNumber = TryParseInt((string?)rowElement.Attribute("r")) ?? fallbackRowNumber;
            fallbackRowNumber = rowNumber + 1;

            var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var fallbackColumnIndex = 1;
            foreach (var cell in rowElement.Elements(SpreadsheetNs + "c"))
            {
                var reference = (string?)cell.Attribute("r");
                var column = !string.IsNullOrWhiteSpace(reference)
                    ? ExtractColumnLetter(reference)
                    : ColumnName(fallbackColumnIndex);

                fallbackColumnIndex = ColumnLetterToIndex(column) + 2;
                var value = ReadCellValue(cell, sharedStrings);
                if (value is not null)
                    row[column] = value;
            }

            while (rows.Count < rowNumber - 1)
                rows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));

            if (rows.Count == rowNumber - 1)
                rows.Add(row);
            else
                rows[rowNumber - 1] = row;
        }

        return rows;
    }

    private static IReadOnlyList<SheetRef> ReadWorkbookSheets(byte[] fileBytes)
    {
        using var stream = new MemoryStream(fileBytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        return ReadWorkbookSheets(archive);
    }

    private static IReadOnlyList<SheetRef> ReadWorkbookSheets(ZipArchive archive)
    {
        var workbook = LoadXmlEntry(archive, "xl/workbook.xml")
            ?? throw new InvalidDataException("Missing xl/workbook.xml.");
        var relationships = LoadXmlEntry(archive, "xl/_rels/workbook.xml.rels")
            ?? throw new InvalidDataException("Missing xl/_rels/workbook.xml.rels.");

        var relationshipTargets = relationships.Root?
            .Elements(PackageRelationshipNs + "Relationship")
            .Where(e => ((string?)e.Attribute("Type"))?.EndsWith("/worksheet", StringComparison.OrdinalIgnoreCase) == true)
            .Select(e => new
            {
                Id = (string?)e.Attribute("Id"),
                Target = (string?)e.Attribute("Target"),
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Target))
            .ToDictionary(x => x.Id!, x => NormalizePackagePath(x.Target!), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var result = new List<SheetRef>();
        var sheetElements = workbook.Root?
            .Element(SpreadsheetNs + "sheets")?
            .Elements(SpreadsheetNs + "sheet") ?? [];

        foreach (var sheet in sheetElements)
        {
            var name = (string?)sheet.Attribute("name");
            var relationshipId = (string?)sheet.Attribute(OfficeRelationshipNs + "id");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(relationshipId))
                continue;
            if (!relationshipTargets.TryGetValue(relationshipId, out var path))
                continue;

            result.Add(new SheetRef(name, relationshipId, path));
        }

        return result;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var document = LoadXmlEntry(archive, "xl/sharedStrings.xml");
        if (document?.Root is null) return [];

        return document.Root
            .Elements(SpreadsheetNs + "si")
            .Select(si => string.Concat(si.Descendants(SpreadsheetNs + "t").Select(t => (string?)t ?? string.Empty)))
            .ToList();
    }

    private static XDocument? LoadXmlEntry(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        if (entry is null) return null;
        using var entryStream = entry.Open();
        return XDocument.Load(entryStream, LoadOptions.None);
    }

    private static object? ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
            return ReadInlineString(cell);

        var raw = (string?)cell.Element(SpreadsheetNs + "v");
        if (string.IsNullOrEmpty(raw))
            return null;

        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase))
        {
            var index = TryParseInt(raw);
            return index is not null && index.Value >= 0 && index.Value < sharedStrings.Count
                ? sharedStrings[index.Value]
                : raw;
        }

        if (string.Equals(type, "b", StringComparison.OrdinalIgnoreCase))
            return raw == "1" ? "TRUE" : "FALSE";

        if (string.Equals(type, "d", StringComparison.OrdinalIgnoreCase)
            && DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
        {
            return date;
        }

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : raw;
    }

    private static string? ReadInlineString(XElement cell)
    {
        var inlineString = cell.Element(SpreadsheetNs + "is");
        if (inlineString is null) return null;
        var text = string.Concat(inlineString.Descendants(SpreadsheetNs + "t").Select(t => (string?)t ?? string.Empty));
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static string NormalizePackagePath(string target)
    {
        var normalized = target.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal))
            return normalized.TrimStart('/');

        var parts = new List<string>();
        foreach (var part in ("xl/" + normalized).Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
                continue;
            }

            parts.Add(part);
        }

        return string.Join('/', parts);
    }

    private static int? TryParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static string ExtractColumnLetter(string cellReference)
    {
        var letters = new string(cellReference.TakeWhile(char.IsLetter).ToArray());
        return string.IsNullOrEmpty(letters) ? "A" : letters;
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
        if (value is double serial && TryParseExcelSerialDate(serial, out var serialDate))
        {
            year = serialDate.Year;
            month = serialDate.Month;
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
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            year = parsed.Year;
            month = parsed.Month;
            return true;
        }
        return false;
    }

    private static bool TryParseExcelSerialDate(double serial, out DateTime date)
    {
        date = default;
        if (serial is < 20000 or > 80000) return false;

        try
        {
            date = DateTime.FromOADate(serial);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

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

    private static bool TryParseDay(string text, out int day)
    {
        day = 0;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d))
            return false;
        if (d is < 1 or > 31) return false;
        day = d;
        return true;
    }

    private static bool TryParseScheduleTitle(string? text, out int year, out int month)
    {
        year = 0;
        month = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!text.Contains("PHÂN CA", StringComparison.OrdinalIgnoreCase)) return false;

        var match = MonthInTitleRegex.Match(text);
        if (!match.Success) return false;
        if (!int.TryParse(match.Groups["year"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out year))
            return false;
        if (!int.TryParse(match.Groups["month"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out month))
            return false;

        return month is >= 1 and <= 12;
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
