using System.IO.Compression;
using System.Text;

namespace MPS.API.Services;

/// <summary>
/// Sinh file .xlsx template cho import ngày lễ. Layout đơn sheet:
///   Row 1: header 4 cột (Tên ngày lễ | Từ ngày | Đến ngày | Mô tả)
///   Row 2-4: ví dụ tham khảo
/// Không đặt ghi chú trong sheet dữ liệu vì parser sẽ đọc chúng thành row lỗi.
/// </summary>
internal static class PublicHolidayImportTemplateGenerator
{
    public const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Generate()
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", BuildContentTypes());
            WriteEntry(archive, "_rels/.rels", BuildRootRelationships());
            WriteEntry(archive, "xl/workbook.xml", BuildWorkbook());
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships());
            WriteEntry(archive, "xl/styles.xml", BuildStyles());
            WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheet());
        }

        return output.ToArray();
    }

    private static string BuildWorksheet()
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">""");
        sb.Append("""<dimension ref="A1:D4"/>""");
        sb.Append("""<sheetViews><sheetView workbookViewId="0"/></sheetViews>""");
        sb.Append("""<sheetFormatPr defaultRowHeight="18"/>""");
        sb.Append("""<cols><col min="1" max="1" width="30" customWidth="1"/><col min="2" max="3" width="14" customWidth="1"/><col min="4" max="4" width="42" customWidth="1"/></cols>""");
        sb.Append("""<sheetData>""");

        WriteRow(sb, 1, [
            InlineCell("A1", "Tên ngày lễ", 1),
            InlineCell("B1", "Từ ngày", 1),
            InlineCell("C1", "Đến ngày", 1),
            InlineCell("D1", "Mô tả", 1)
        ], height: 22);

        WriteRow(sb, 2, [
            InlineCell("A2", "Tết Dương lịch", 2),
            InlineCell("B2", "01/01/2027", 2),
            InlineCell("C2", "01/01/2027", 2),
            InlineCell("D2", "Nghỉ Tết dương lịch", 2)
        ]);
        WriteRow(sb, 3, [
            InlineCell("A3", "Tết Nguyên đán", 2),
            InlineCell("B3", "05/02/2027", 2),
            InlineCell("C3", "11/02/2027", 2),
            InlineCell("D3", "Đợt nghỉ 7 ngày", 2)
        ]);
        WriteRow(sb, 4, [
            InlineCell("A4", "Giỗ tổ Hùng Vương", 2),
            InlineCell("B4", "16/04/2027", 2),
            EmptyCell("C4", 2),
            InlineCell("D4", "Bỏ trống 'Đến ngày' cho đợt 1 ngày", 2)
        ]);

        sb.Append("""</sheetData>""");
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

    private static string EmptyCell(string reference, int style) =>
        $"""<c r="{reference}" s="{style}"/>""";

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
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Ngày lễ" sheetId="1" r:id="rId1"/></sheets></workbook>""";

    private static string BuildWorkbookRelationships() =>
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""";

    private static string BuildStyles() =>
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="3"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font><font><i/><sz val="10"/><color rgb="FF666666"/><name val="Calibri"/></font></fonts><fills count="4"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF1F4E78"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFF2F2F2"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="2"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style="thin"><color rgb="FFD9D9D9"/></left><right style="thin"><color rgb="FFD9D9D9"/></right><top style="thin"><color rgb="FFD9D9D9"/></top><bottom style="thin"><color rgb="FFD9D9D9"/></bottom><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="5"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf><xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment vertical="center"/></xf><xf numFmtId="0" fontId="2" fillId="3" borderId="0" xfId="0" applyFont="1" applyFill="1"/><xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""";
}
