using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;

namespace MPS.Infrastructure.Services;

/// <summary>
/// Parser xlsx cho import ngày lễ. Đọc trực tiếp zip+xml, không phụ thuộc thư viện Excel.
///
/// Layout kỳ vọng (sheet đầu tiên):
///   Row 1: header — chấp nhận alias tiếng Việt/Anh cho các cột:
///     - Tên (name): "Tên ngày lễ" | "Tên" | "Holiday Name" | "Name"
///     - Từ ngày (start): "Từ ngày" | "Ngày" | "Ngày bắt đầu" | "Holiday Date" | "Start Date"
///     - Đến ngày (end, optional): "Đến ngày" | "Ngày kết thúc" | "End Date"
///     - Mô tả (desc, optional): "Mô tả" | "Description" | "Note"
///   Row 2..N: dữ liệu.
///
/// Ngày chấp nhận cả DateTime (Excel serial) lẫn text "yyyy-MM-dd" / "dd/MM/yyyy" / "d/M/yyyy".
/// Description và Đến ngày bỏ trống được (đến ngày trống ⇒ = từ ngày).
/// </summary>
public sealed class PublicHolidayExcelParser : IPublicHolidayExcelParser
{
    private static readonly XNamespace SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    private static readonly string[] NameAliases =
        ["Tên ngày lễ", "Tên", "Holiday Name", "Name"];
    private static readonly string[] StartDateAliases =
        ["Từ ngày", "Ngày", "Ngày bắt đầu", "Holiday Date", "Start Date"];
    private static readonly string[] EndDateAliases =
        ["Đến ngày", "Ngày kết thúc", "End Date", "Holiday End Date"];
    private static readonly string[] DescriptionAliases =
        ["Mô tả", "Description", "Note"];

    // Row bắt đầu bằng các marker này + không có ngày → là dòng ghi chú, skip khỏi data.
    private static readonly string[] NoteMarkers =
        ["Ghi chú", "Note", "Chú thích", "•", "*", "-"];

    public PublicHolidayImportParseResult Parse(Stream stream)
    {
        var globalErrors = new List<PublicHolidayImportRowError>();
        var rows = new List<PublicHolidayImportParsedRow>();

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
            globalErrors.Add(new PublicHolidayImportRowError(null, "INVALID_XLSX",
                $"Không đọc được file ({ex.GetType().Name}: {ex.Message})."));
            return new PublicHolidayImportParseResult(rows, globalErrors);
        }

        IReadOnlyList<SheetRef> sheets;
        try
        {
            sheets = ReadWorkbookSheets(fileBytes);
        }
        catch (Exception ex)
        {
            globalErrors.Add(new PublicHolidayImportRowError(null, "INVALID_XLSX",
                $"File không phải xlsx hợp lệ ({ex.GetType().Name}: {ex.Message})."));
            return new PublicHolidayImportParseResult(rows, globalErrors);
        }

        if (sheets.Count == 0)
        {
            globalErrors.Add(new PublicHolidayImportRowError(null, "NO_SHEET", "File excel không có worksheet nào."));
            return new PublicHolidayImportParseResult(rows, globalErrors);
        }

        List<IDictionary<string, object>> sheetRows;
        try
        {
            sheetRows = QueryRows(fileBytes, sheets[0].Name);
        }
        catch (Exception ex)
        {
            globalErrors.Add(new PublicHolidayImportRowError(null, "INVALID_XLSX",
                $"Không đọc được nội dung sheet ({ex.GetType().Name}: {ex.Message})."));
            return new PublicHolidayImportParseResult(rows, globalErrors);
        }

        if (sheetRows.Count == 0)
        {
            globalErrors.Add(new PublicHolidayImportRowError(null, "EMPTY_SHEET", "Sheet rỗng."));
            return new PublicHolidayImportParseResult(rows, globalErrors);
        }

        var headerRow = sheetRows[0];
        string? nameCol = null, startCol = null, endCol = null, descCol = null;
        foreach (var (letter, raw) in headerRow)
        {
            var text = ToTrimmedString(raw);
            if (string.IsNullOrEmpty(text)) continue;

            if (nameCol is null && IsAnyAlias(text, NameAliases)) nameCol = letter;
            else if (startCol is null && IsAnyAlias(text, StartDateAliases)) startCol = letter;
            else if (endCol is null && IsAnyAlias(text, EndDateAliases)) endCol = letter;
            else if (descCol is null && IsAnyAlias(text, DescriptionAliases)) descCol = letter;
        }

        if (nameCol is null)
            globalErrors.Add(new PublicHolidayImportRowError(1, "MISSING_NAME_HEADER",
                "Header thiếu cột 'Tên ngày lễ'."));
        if (startCol is null)
            globalErrors.Add(new PublicHolidayImportRowError(1, "MISSING_START_HEADER",
                "Header thiếu cột 'Từ ngày'."));

        if (nameCol is null || startCol is null)
            return new PublicHolidayImportParseResult(rows, globalErrors);

        for (var r = 1; r < sheetRows.Count; r++)
        {
            var row = sheetRows[r];
            var rowIndex = r + 1;
            var rowErrors = new List<PublicHolidayImportRowError>();

            var name = GetCellString(row, nameCol);
            var startRaw = row.TryGetValue(startCol, out var sVal) ? sVal : null;
            var endRaw = endCol is not null && row.TryGetValue(endCol, out var eVal) ? eVal : null;
            var desc = descCol is not null ? GetCellString(row, descCol) : null;

            // Skip fully-empty row
            if (string.IsNullOrEmpty(name) && startRaw is null && endRaw is null && string.IsNullOrEmpty(desc))
                continue;

            // Skip dòng ghi chú (marker + không có ngày) — không báo lỗi.
            if (startRaw is null && !string.IsNullOrEmpty(name) && IsNoteMarker(name))
                continue;

            if (string.IsNullOrWhiteSpace(name))
                rowErrors.Add(new PublicHolidayImportRowError(rowIndex, "MISSING_NAME", "Thiếu tên ngày lễ."));

            DateOnly? start = null;
            if (startRaw is null)
            {
                rowErrors.Add(new PublicHolidayImportRowError(rowIndex, "MISSING_START_DATE", "Thiếu 'Từ ngày'."));
            }
            else if (!TryParseDate(startRaw, out var parsedStart))
            {
                rowErrors.Add(new PublicHolidayImportRowError(rowIndex, "INVALID_START_DATE",
                    $"'Từ ngày' không hợp lệ: '{ToTrimmedString(startRaw)}'."));
            }
            else
            {
                start = parsedStart;
            }

            DateOnly? end = null;
            if (endRaw is not null)
            {
                if (!TryParseDate(endRaw, out var parsedEnd))
                    rowErrors.Add(new PublicHolidayImportRowError(rowIndex, "INVALID_END_DATE",
                        $"'Đến ngày' không hợp lệ: '{ToTrimmedString(endRaw)}'."));
                else
                    end = parsedEnd;
            }

            if (start.HasValue && end.HasValue && end.Value < start.Value)
                rowErrors.Add(new PublicHolidayImportRowError(rowIndex, "END_BEFORE_START",
                    "'Đến ngày' phải sau hoặc bằng 'Từ ngày'."));

            rows.Add(new PublicHolidayImportParsedRow(
                rowIndex,
                string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
                start,
                end,
                string.IsNullOrWhiteSpace(desc) ? null : desc.Trim(),
                rowErrors));
        }

        return new PublicHolidayImportParseResult(rows, globalErrors);
    }

    // ─── xlsx helpers (rút gọn từ MonthlyScheduleExcelParser) ──────────────────

    private sealed record SheetRef(string Name, string Path);

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

            result.Add(new SheetRef(name, path));
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
        if (value is DateTime dt) return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (value is double d)
        {
            return d == Math.Floor(d) && d is >= long.MinValue and <= long.MaxValue
                ? ((long)d).ToString(CultureInfo.InvariantCulture)
                : d.ToString(CultureInfo.InvariantCulture);
        }
        return value.ToString()?.Trim();
    }

    private static bool TryParseDate(object? value, out DateOnly result)
    {
        result = default;
        if (value is null) return false;

        if (value is DateTime dt)
        {
            result = DateOnly.FromDateTime(dt);
            return true;
        }
        if (value is double serial && TryParseExcelSerialDate(serial, out var serialDate))
        {
            result = DateOnly.FromDateTime(serialDate);
            return true;
        }
        var text = value is string s ? s.Trim() : value.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return false;

        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "yyyy/MM/dd" };
        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            result = DateOnly.FromDateTime(parsed);
            return true;
        }
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            result = DateOnly.FromDateTime(parsed);
            return true;
        }
        return false;
    }

    private static bool TryParseExcelSerialDate(double serial, out DateTime date)
    {
        date = default;
        if (serial is < 20000 or > 80000) return false;
        try { date = DateTime.FromOADate(serial); return true; }
        catch (ArgumentException) { return false; }
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

    private static bool IsAnyAlias(string? value, IReadOnlyList<string> aliases) =>
        !string.IsNullOrEmpty(value)
        && aliases.Any(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase));

    private static bool IsNoteMarker(string value) =>
        NoteMarkers.Any(m => value.StartsWith(m, StringComparison.OrdinalIgnoreCase));
}
