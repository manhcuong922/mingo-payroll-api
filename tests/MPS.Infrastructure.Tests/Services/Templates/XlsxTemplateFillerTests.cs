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

    /// <summary>
    /// Đọc giá trị cache (&lt;v&gt;) của một ô — thứ mà mọi trình xem KHÔNG tự tính lại công thức
    /// sẽ hiển thị. Đây chính là chỗ trước đây trả về 0 làm chứng từ in ra trống.
    /// </summary>
    private static (string? Cached, string? Formula) ReadFormulaCell(
        byte[] xlsx, string sheetName, string cellRef)
    {
        using var ms = new MemoryStream(xlsx);
        using var doc = SpreadsheetDocument.Open(ms, false);
        var workbookPart = doc.WorkbookPart!;
        var sheet = workbookPart.Workbook.Descendants<Sheet>().First(s => s.Name == sheetName);
        var part = (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);

        var cell = part.Worksheet.Descendants<Cell>()
            .FirstOrDefault(c => c.CellReference?.Value == cellRef);

        return (cell?.CellValue?.InnerText, cell?.CellFormula?.Text);
    }

    [Fact]
    public void Fill_GhiCacheChoOCongThucTroToiOVuaDien()
    {
        // AB22 (liên 1) và AB63 (liên 2) đều là "=Nguon!$B$24" — ô tiền của bản in.
        var result = FillBhxhTemplate(XlsxCellEdit.OfNumber(DataSheet, "B24", 76_189_773m));

        foreach (var cellRef in new[] { "AB22", "AB63" })
        {
            var (cached, formula) = ReadFormulaCell(result, FormSheet, cellRef);

            formula.Should().Be("Nguon!$B$24", "công thức của biểu mẫu phải còn nguyên");
            cached.Should().Be("76189773",
                "trình xem không tính lại công thức chỉ đọc cache — cache sai là chứng từ sai tiền");
        }
    }

    [Fact]
    public void Fill_GhiCacheChoCaCongThucNoiChuoiVaTEXT()
    {
        var result = FillBhxhTemplate(
            XlsxCellEdit.OfText(DataSheet, "B11", "BHXH-07/2026"),
            XlsxCellEdit.OfDate(DataSheet, "B12", new DateTime(2026, 7, 31)));

        // AB1  = "Số: "&Nguon!$B$11
        ReadFormulaCell(result, FormSheet, "AB1").Cached.Should().Be("Số: BHXH-07/2026");

        // N5   = "Ngày: "&TEXT(Nguon!$B$12,"dd/mm/yyyy") — 'm' của Excel là THÁNG, không phải phút.
        ReadFormulaCell(result, FormSheet, "N5").Cached.Should().Be("Ngày: 31/07/2026");
    }

    [Fact]
    public void Fill_KhongDienGiOCa_ThiKhongDungVaoCacheCuaBieuMau()
    {
        // Không có ô nào được ghi ⇒ không suy ra được gì ⇒ phải để nguyên biểu mẫu.
        using var template = Templates.Open(PaymentTemplateKind.BhxhRemittanceOrder);
        var result = Filler.Fill(template, []);

        var (cached, formula) = ReadFormulaCell(result, FormSheet, "AB22");

        formula.Should().Be("Nguon!$B$24");
        cached.Should().Be("0", "cache gốc của biểu mẫu phải được giữ nguyên khi không ghi gì");
    }

    [Fact]
    public void Fill_OCongThucKhongLienQuanDuLieuGhiVao_ThiGiuNguyenCache()
    {
        // Chỉ ghi B24; các ô công thức trỏ vào B38/B39/... không được đụng tới.
        var result = FillBhxhTemplate(XlsxCellEdit.OfNumber(DataSheet, "B24", 1_000m));

        // H8 = Nguon!$B$38 — không nằm trong danh sách ô vừa ghi.
        var (cached, formula) = ReadFormulaCell(result, FormSheet, "H8");

        formula.Should().Be("Nguon!$B$38");
        cached.Should().Be("0");
    }

    [Fact]
    public void Fill_GhiOThanhTrong_ThiXoaCacheChuKhongDeSoCu()
    {
        // Ghi rỗng = chưa khai. Để nguyên cache cũ thì bản in hiện số của lần lưu trước —
        // với chứng từ chuyển tiền, ô trống an toàn hơn số sai.
        var result = FillBhxhTemplate(XlsxCellEdit.OfText(DataSheet, "B24", null));

        var (cached, formula) = ReadFormulaCell(result, FormSheet, "AB22");

        formula.Should().Be("Nguon!$B$24");
        cached.Should().BeNull();
    }

    [Fact]
    public void Fill_MauLenhChiLuongKhongCoCongThuc_NenKhongBiAnhHuong()
    {
        // Mẫu VPBank ghi số thẳng vào ô in (0 công thức) — thay đổi về cache không được
        // làm suy suyển gì ở đây.
        using var template = Templates.Open(PaymentTemplateKind.SalaryPaymentOrder);
        var result = Filler.Fill(template, [XlsxCellEdit.OfNumber("Lệnh TT", "A1", 1m)]);

        using var ms = new MemoryStream(result);
        using var doc = SpreadsheetDocument.Open(ms, false);

        doc.WorkbookPart!.WorksheetParts
            .SelectMany(p => p.Worksheet.Descendants<Cell>())
            .Count(c => c.CellFormula is not null)
            .Should().Be(0);
    }
}
