using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FluentAssertions;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Infrastructure.Services.Templates;
using Xunit;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace MPS.Infrastructure.Tests.Services.Templates;

/// <summary>
/// Chèn dòng vào giữa mẫu lệnh chi lương VPBank.
///
/// <para>Chỉ số dòng bị nhắc tới ở 8 chỗ rải trên 4 phần XML. Mỗi test dưới đây canh MỘT chỗ:
/// bỏ sót chỗ nào thì file vẫn mở được nhưng chứng từ in ra sai — ngắt trang giữa bảng, logo
/// đè lên chữ, vùng in cụt mất phần chữ ký.</para>
/// </summary>
public class XlsxRowInserterTests
{
    private static readonly IPaymentTemplateProvider Templates = new EmbeddedPaymentTemplateProvider();
    private static readonly IXlsxTemplateFiller Filler = new XlsxTemplateFiller();

    private const string Sheet = "Lệnh TT";

    /// <summary>Bảng 1 (TK tại VPBank) chừa sẵn dòng 30-32; chèn thêm ngay sau dòng 32.</summary>
    private const uint Table1FirstDataRow = 30;
    private const uint Table1LastDataRow = 32;

    private const int Added = 10;

    private static byte[] InsertIntoTable1(int count = Added)
        => InsertIntoTable1(count, []);

    private static byte[] InsertIntoTable1(int count, IReadOnlyCollection<XlsxCellEdit> edits)
    {
        using var template = Templates.Open(PaymentTemplateKind.SalaryPaymentOrder);
        return Filler.Fill(
            template,
            [new XlsxRowInsertion(Sheet, Table1LastDataRow + 1, count, Table1LastDataRow)],
            edits);
    }

    private static (SpreadsheetDocument Doc, WorksheetPart Part) Open(byte[] xlsx, MemoryStream ms)
    {
        ms.Write(xlsx, 0, xlsx.Length);
        ms.Position = 0;
        var doc = SpreadsheetDocument.Open(ms, false);
        var workbookPart = doc.WorkbookPart!;
        var sheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == Sheet);
        return (doc, (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!));
    }

    private static string? TextOf(WorksheetPart part, string cellRef)
    {
        var cell = part.Worksheet.Descendants<Cell>()
            .FirstOrDefault(c => c.CellReference?.Value == cellRef);
        return cell?.InlineString?.InnerText ?? cell?.CellValue?.InnerText;
    }

    // ===== 1. Dòng và ô =====

    [Fact]
    public void Insert_DichChiSoDongPhiaDuoi()
    {
        var result = InsertIntoTable1();

        using var ms = new MemoryStream();
        var (doc, part) = Open(result, ms);
        using var _ = doc;

        // "Tổng cộng:" vốn ở A38 phải trôi xuống A48.
        TextOf(part, "A38").Should().NotBe("Tổng cộng:");
        part.Worksheet.Descendants<Row>().Select(r => r.RowIndex!.Value)
            .Should().Contain(48u);
    }

    [Fact]
    public void Insert_KhongTaoDongTrungChiSo()
    {
        var result = InsertIntoTable1();

        using var ms = new MemoryStream();
        var (doc, part) = Open(result, ms);
        using var _ = doc;

        var indices = part.Worksheet.Descendants<Row>().Select(r => r.RowIndex!.Value).ToList();

        indices.Should().OnlyHaveUniqueItems();
        indices.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Insert_DongMoiCoDuOVaGiuDinhDangDongMau()
    {
        var result = InsertIntoTable1();

        using var ms = new MemoryStream();
        var (doc, part) = Open(result, ms);
        using var _ = doc;

        var donor = part.Worksheet.Descendants<Row>().First(r => r.RowIndex!.Value == Table1LastDataRow);
        var inserted = part.Worksheet.Descendants<Row>().First(r => r.RowIndex!.Value == 33);

        inserted.StyleIndex?.Value.Should().Be(donor.StyleIndex?.Value);
        (inserted.CustomFormat?.Value).Should().Be(donor.CustomFormat?.Value);
        inserted.Elements<Cell>().Select(c => c.StyleIndex?.Value)
            .Should().BeEquivalentTo(donor.Elements<Cell>().Select(c => c.StyleIndex?.Value));
    }

    [Fact]
    public void Insert_DongMoiKhongMangNoiDungCuaDongMau()
    {
        // Dòng mẫu của bảng 1 có sẵn chữ "VPBANK" ở cột ngân hàng — bê nguyên sang dòng mới
        // là mọi dòng đều hiện VPBANK kể cả khi chưa điền gì.
        var result = InsertIntoTable1();

        using var ms = new MemoryStream();
        var (doc, part) = Open(result, ms);
        using var _ = doc;

        var inserted = part.Worksheet.Descendants<Row>().First(r => r.RowIndex!.Value == 33);

        inserted.Elements<Cell>().Should().OnlyContain(c =>
            c.CellValue == null && c.InlineString == null);
    }

    // ===== 2. Vùng gộp ô =====

    [Fact]
    public void Insert_DichVungGopONamDuoiChoChen()
    {
        var result = InsertIntoTable1();

        using var ms = new MemoryStream();
        var (doc, part) = Open(result, ms);
        using var _ = doc;

        var merges = part.Worksheet.Descendants<MergeCell>()
            .Select(m => m.Reference!.Value!).ToList();

        merges.Should().Contain("A50:I50", "vùng A40:I40 phải trôi xuống 10 dòng");
        merges.Should().Contain("G76:I77", "vùng G66:I67 phải trôi xuống 10 dòng");
        merges.Should().Contain("A2:I2", "vùng phía trên chỗ chèn phải giữ nguyên");
        merges.Should().Contain("F32:H32", "dòng mẫu phía trên chỗ chèn phải giữ nguyên");
    }

    [Fact]
    public void Insert_NhanBanVungGopChoTungDongMoi()
    {
        // Bảng 1 gộp F:H mỗi dòng; thiếu bước này thì dòng mới lệch cột so với dòng có sẵn.
        var result = InsertIntoTable1();

        using var ms = new MemoryStream();
        var (doc, part) = Open(result, ms);
        using var _ = doc;

        var merges = part.Worksheet.Descendants<MergeCell>()
            .Select(m => m.Reference!.Value!).ToList();

        for (var row = 33; row < 33 + Added; row++)
        {
            merges.Should().Contain($"F{row}:H{row}", "dòng mới {0} phải có vùng gộp F:H", row);
        }
    }

    [Fact]
    public void Insert_CapNhatSoLuongVungGop()
    {
        var result = InsertIntoTable1();

        using var ms = new MemoryStream();
        var (doc, part) = Open(result, ms);
        using var _ = doc;

        var mergeCells = part.Worksheet.GetFirstChild<MergeCells>()!;

        mergeCells.Count!.Value.Should().Be((uint)mergeCells.Elements<MergeCell>().Count());
    }

    // ===== 3. Khung kích thước =====

    [Fact]
    public void Insert_MoRongSheetDimension()
    {
        var result = InsertIntoTable1();

        using var ms = new MemoryStream();
        var (doc, part) = Open(result, ms);
        using var _ = doc;

        part.Worksheet.GetFirstChild<SheetDimension>()!.Reference!.Value
            .Should().Be("A1:L86", "gốc là A1:L76");
    }

    // ===== 4. Ngắt trang thủ công =====

    [Fact]
    public void Insert_DichNgatTrangThuCong()
    {
        // Gốc có ngắt trang ở dòng 52; không dịch thì trang bị cắt ngay giữa danh sách NV.
        var result = InsertIntoTable1();

        using var ms = new MemoryStream();
        var (doc, part) = Open(result, ms);
        using var _ = doc;

        part.Worksheet.GetFirstChild<RowBreaks>()!
            .Elements<Break>().Select(b => b.Id!.Value)
            .Should().Contain(62u, "ngắt trang dòng 52 phải trôi xuống 62");
    }

    // ===== 5. Vùng in =====

    [Fact]
    public void Insert_MoRongVungIn()
    {
        // Không mở rộng Print_Area thì phần cam kết + chữ ký bị cắt khỏi bản in.
        var result = InsertIntoTable1();

        using var ms = new MemoryStream();
        var (doc, _) = Open(result, ms);
        using var __ = doc;

        var printArea = doc.WorkbookPart!.Workbook.GetFirstChild<DefinedNames>()!
            .Elements<DefinedName>().First(d => d.Name == "_xlnm.Print_Area");

        printArea.Text.Should().Contain("$I$86", "gốc là $A$1:$I$76");
    }

    [Fact]
    public void Insert_KhongDungVaoThamChieuDaHong()
    {
        // Mẫu gốc có sẵn vài defined name trỏ #REF!; đụng vào chỉ làm hỏng thêm.
        var result = InsertIntoTable1();

        using var ms = new MemoryStream();
        var (doc, _) = Open(result, ms);
        using var __ = doc;

        var names = doc.WorkbookPart!.Workbook.GetFirstChild<DefinedNames>()!
            .Elements<DefinedName>().Where(d => d.Text.Contains("#REF!"));

        names.Should().NotBeEmpty();
        names.Should().OnlyContain(d => d.Text.Contains("#REF!"));
    }

    // ===== 6-7. Chú thích và hình VML =====

    [Fact]
    public void Insert_DichONeoChuThich()
    {
        var result = InsertIntoTable1();

        using var ms = new MemoryStream();
        var (doc, part) = Open(result, ms);
        using var _ = doc;

        var refs = part.WorksheetCommentsPart!.Comments!.Descendants<Comment>()
            .Select(c => c.Reference!.Value!).ToList();

        refs.Should().Contain("A11", "chú thích phía trên chỗ chèn giữ nguyên");
        refs.Should().Contain("C68", "chú thích ở C58 phải trôi xuống C68");
    }

    [Fact]
    public void Insert_DichNeoHinhVml()
    {
        var result = InsertIntoTable1();

        using var ms = new MemoryStream();
        var (doc, part) = Open(result, ms);
        using var _ = doc;

        var vmlPart = part.VmlDrawingParts.First();
        using var reader = new StreamReader(vmlPart.GetStream(FileMode.Open, FileAccess.Read));
        var rows = Regex.Matches(reader.ReadToEnd(), @"<x:Row>(\d+)</x:Row>")
            .Select(m => int.Parse(m.Groups[1].Value)).ToList();

        // Neo đếm từ 0: gốc là 10 (dòng 11, phía trên) và 57 (dòng 58, phía dưới).
        rows.Should().Contain(10);
        rows.Should().Contain(67);
    }

    // ===== 8. Ảnh neo theo ô =====

    [Fact]
    public void Insert_DichNeoAnhTrongDrawing()
    {
        // Logo neo ở dòng 46; không dịch thì ảnh đè lên danh sách nhân viên.
        var result = InsertIntoTable1();

        using var ms = new MemoryStream();
        var (doc, part) = Open(result, ms);
        using var _ = doc;

        var rows = part.DrawingsPart!.WorksheetDrawing!.Descendants<Xdr.RowId>()
            .Select(r => int.Parse(r.Text)).ToList();

        rows.Should().Contain(55, "neo ở dòng 45 (0-based) phải thành 55");
        rows.Should().NotContain(45);
    }

    // ===== Kết hợp chèn + ghi ô =====

    [Fact]
    public void Insert_RoiGhiO_GhiDungVaoDongMoi()
    {
        var result = InsertIntoTable1(Added,
        [
            XlsxCellEdit.OfText(Sheet, "B33", "Nguyễn Văn Minh"),
            XlsxCellEdit.OfNumber(Sheet, "D33", 7_500_000m),
            XlsxCellEdit.OfText(Sheet, "B42", "Trần Thị Hoa"),
        ]);

        using var ms = new MemoryStream();
        var (doc, part) = Open(result, ms);
        using var _ = doc;

        TextOf(part, "B33").Should().Be("Nguyễn Văn Minh");
        TextOf(part, "D33").Should().Be("7500000");
        TextOf(part, "B42").Should().Be("Trần Thị Hoa");
    }

    [Fact]
    public void Insert_HaiBangCungLuc_ToaDoTinhTheoBieuMauGoc()
    {
        // Bảng 2 (dòng 35-37) chèn trước, bảng 1 (dòng 30-32) chèn sau — bên gọi vẫn khai
        // toạ độ gốc, lớp filler tự áp dụng từ dưới lên.
        using var template = Templates.Open(PaymentTemplateKind.SalaryPaymentOrder);
        var result = Filler.Fill(
            template,
            [
                new XlsxRowInsertion(Sheet, 33, 5, 32),   // bảng 1: +5
                new XlsxRowInsertion(Sheet, 38, 4, 37),   // bảng 2: +4
            ],
            []);

        using var ms = new MemoryStream();
        var (doc, part) = Open(result, ms);
        using var _ = doc;

        var indices = part.Worksheet.Descendants<Row>().Select(r => r.RowIndex!.Value).ToList();
        indices.Should().OnlyHaveUniqueItems();
        indices.Should().BeInAscendingOrder();

        // 76 + 5 + 4 = 85
        part.Worksheet.GetFirstChild<SheetDimension>()!.Reference!.Value.Should().Be("A1:L85");
    }

    [Fact]
    public void Insert_SoLuongBangKhong_ThiKhongDoiGi()
    {
        using var template = Templates.Open(PaymentTemplateKind.SalaryPaymentOrder);
        var result = Filler.Fill(template, [new XlsxRowInsertion(Sheet, 33, 0, 32)], []);

        using var ms = new MemoryStream();
        var (doc, part) = Open(result, ms);
        using var _ = doc;

        part.Worksheet.GetFirstChild<SheetDimension>()!.Reference!.Value.Should().Be("A1:L76");
    }
}
