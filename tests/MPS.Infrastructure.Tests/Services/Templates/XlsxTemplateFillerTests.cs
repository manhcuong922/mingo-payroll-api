using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FluentAssertions;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Infrastructure.Services.Templates;
using Xunit;

namespace MPS.Infrastructure.Tests.Services.Templates;

/// <summary>
/// Chạy thẳng trên biểu mẫu Agribank nhúng thật, không dùng workbook giả — vì rủi ro lớn nhất
/// là biểu mẫu ngân hàng bị vỡ (mất công thức, mất merge, mất định dạng) chứ không phải logic ghi ô.
/// </summary>
public class XlsxTemplateFillerTests
{
    private static readonly IPaymentTemplateProvider Templates = new EmbeddedPaymentTemplateProvider();
    private static readonly IXlsxTemplateFiller Filler = new XlsxTemplateFiller();

    private const string DataSheet = "Nguon";
    private const string FormSheet = "UNC";

    private static byte[] FillBhxhTemplate(params XlsxCellEdit[] edits)
    {
        using var template = Templates.Open(PaymentTemplateKind.BhxhRemittanceOrder);
        return Filler.Fill(template, edits);
    }

    private static string? ReadCellText(byte[] xlsx, string sheetName, string cellRef)
    {
        using var ms = new MemoryStream(xlsx);
        using var doc = SpreadsheetDocument.Open(ms, false);
        var workbookPart = doc.WorkbookPart!;
        var sheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == sheetName);
        var part = (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);

        var cell = part.Worksheet.Descendants<Cell>()
            .FirstOrDefault(c => c.CellReference?.Value == cellRef);

        if (cell is null) return null;
        if (cell.InlineString is not null) return cell.InlineString.InnerText;
        return cell.CellValue?.InnerText;
    }

    [Theory]
    [InlineData("A1", 1u, 1u)]
    [InlineData("B23", 23u, 2u)]
    [InlineData("Z9", 9u, 26u)]
    [InlineData("AA1", 1u, 27u)]
    [InlineData("AB22", 22u, 28u)]
    [InlineData("AM80", 80u, 39u)]
    public void ParseReference_DoiDiaChiO_SangDongCot(string reference, uint row, uint column)
    {
        XlsxTemplateFiller.ParseReference(reference).Should().Be((row, column));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("B")]
    [InlineData("B0")]
    [InlineData("1B")]
    public void ParseReference_DiaChiHong_ThiNem(string reference)
    {
        var act = () => XlsxTemplateFiller.ParseReference(reference);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Fill_GhiDuocChuSoVaNgayVaoSheetDuLieu()
    {
        var result = FillBhxhTemplate(
            XlsxCellEdit.OfText(DataSheet, "B23", "+BHXH+103+00+TG14475+00107+dong BHXH+"),
            XlsxCellEdit.OfNumber(DataSheet, "B24", 123_456_789m),
            XlsxCellEdit.OfDate(DataSheet, "B12", new DateTime(2026, 8, 19)));

        ReadCellText(result, DataSheet, "B23").Should().Be("+BHXH+103+00+TG14475+00107+dong BHXH+");
        ReadCellText(result, DataSheet, "B24").Should().Be("123456789");
        ReadCellText(result, DataSheet, "B12")
            .Should().Be(new DateTime(2026, 8, 19).ToOADate().ToString(
                System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Fill_GiuNguyen20CongThucCuaBieuMau()
    {
        // Mẫu UNC gồm 2 liên, mỗi liên 10 ô công thức trỏ sang sheet Nguon.
        // Mất công thức ⇒ chứng từ in ra trống trơn dù dữ liệu đã ghi đúng.
        var result = FillBhxhTemplate(XlsxCellEdit.OfNumber(DataSheet, "B24", 1_000m));

        using var ms = new MemoryStream(result);
        using var doc = SpreadsheetDocument.Open(ms, false);
        var workbookPart = doc.WorkbookPart!;
        var sheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == FormSheet);
        var part = (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);

        var formulas = part.Worksheet.Descendants<Cell>()
            .Where(c => c.CellFormula is not null)
            .Select(c => c.CellFormula!.Text)
            .ToList();

        formulas.Should().HaveCount(20);
        formulas.Should().Contain(f => f.Contains("Nguon!$B$24"));
        formulas.Should().Contain(f => f.Contains("Nguon!$B$23"));
    }

    [Fact]
    public void Fill_GiuNguyenVungMergeCuaBieuMau()
    {
        var result = FillBhxhTemplate(XlsxCellEdit.OfText(DataSheet, "B38", "CÔNG TY MINGO"));

        using var ms = new MemoryStream(result);
        using var doc = SpreadsheetDocument.Open(ms, false);
        var workbookPart = doc.WorkbookPart!;
        var sheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == FormSheet);
        var part = (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);

        part.Worksheet.Descendants<MergeCell>().Should().HaveCount(70);
    }

    [Fact]
    public void Fill_BatTinhLaiToanBoKhiMoFile()
    {
        // Mẫu chạy hoàn toàn bằng công thức: không ép tính lại thì Excel hiện giá trị cache cũ
        // (số liệu của công ty khác) dù dữ liệu nguồn đã bị thay.
        var result = FillBhxhTemplate(XlsxCellEdit.OfNumber(DataSheet, "B24", 5_000m));

        using var ms = new MemoryStream(result);
        using var doc = SpreadsheetDocument.Open(ms, false);
        var workbookPart = doc.WorkbookPart!;

        workbookPart.CalculationChainPart.Should().BeNull();

        var calcProps = workbookPart.Workbook.GetFirstChild<CalculationProperties>();
        calcProps.Should().NotBeNull();
        calcProps!.FullCalculationOnLoad?.Value.Should().BeTrue();
    }

    [Fact]
    public void Fill_GiuNguyenDinhDangCuaOMau()
    {
        // Ô B12 trong mẫu có định dạng ngày; ghi đè mà mất StyleIndex thì Excel hiện số sê-ri thô.
        uint? styleBefore;
        using (var template = Templates.Open(PaymentTemplateKind.BhxhRemittanceOrder))
        {
            using var ms0 = new MemoryStream();
            template.CopyTo(ms0);
            ms0.Position = 0;
            using var doc0 = SpreadsheetDocument.Open(ms0, false);
            var wp0 = doc0.WorkbookPart!;
            var s0 = wp0.Workbook.Descendants<Sheet>().First(s => s.Name == DataSheet);
            var p0 = (WorksheetPart)wp0.GetPartById(s0.Id!.Value!);
            styleBefore = p0.Worksheet.Descendants<Cell>()
                .FirstOrDefault(c => c.CellReference?.Value == "B12")?.StyleIndex?.Value;
        }

        var result = FillBhxhTemplate(
            XlsxCellEdit.OfDate(DataSheet, "B12", new DateTime(2026, 8, 19)));

        using var ms = new MemoryStream(result);
        using var doc = SpreadsheetDocument.Open(ms, false);
        var wp = doc.WorkbookPart!;
        var sheet = wp.Workbook.Descendants<Sheet>().First(s => s.Name == DataSheet);
        var part = (WorksheetPart)wp.GetPartById(sheet.Id!.Value!);
        var styleAfter = part.Worksheet.Descendants<Cell>()
            .First(c => c.CellReference?.Value == "B12").StyleIndex?.Value;

        styleAfter.Should().Be(styleBefore);
    }

    [Fact]
    public void Fill_SheetKhongTonTai_ThiNemKemDanhSachSheetCoThat()
    {
        var act = () => FillBhxhTemplate(XlsxCellEdit.OfText("SheetMaKhong", "A1", "x"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*XLSX_TEMPLATE_SHEET_NOT_FOUND*");
    }

    [Fact]
    public void Fill_KhongSuaFileGoc()
    {
        using var first = Templates.Open(PaymentTemplateKind.BhxhRemittanceOrder);
        Filler.Fill(first, [XlsxCellEdit.OfText(DataSheet, "B38", "GHI LAN 1")]);

        // Lần mở thứ hai phải vẫn là biểu mẫu trắng.
        using var second = Templates.Open(PaymentTemplateKind.BhxhRemittanceOrder);
        var untouched = Filler.Fill(second, []);

        ReadCellText(untouched, DataSheet, "B38").Should().BeNull();
    }

    [Fact]
    public void Open_MoDuocCaBaBieuMauNhung()
    {
        foreach (var kind in Enum.GetValues<PaymentTemplateKind>())
        {
            using var stream = Templates.Open(kind);

            stream.Should().NotBeNull();
            stream.Length.Should().BeGreaterThan(0, "biểu mẫu {0} phải có nội dung", kind);
        }
    }
}
