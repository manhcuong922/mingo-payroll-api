using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FluentAssertions;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Features.Payroll.PaymentDocuments.Common;
using MPS.Infrastructure.Services.Templates;
using Xunit;

namespace MPS.Infrastructure.Tests.Services.Templates;

/// <summary>
/// Chốt hình dạng biểu mẫu ủy nhiệm chi Agribank nhúng trong assembly.
///
/// <para>Code điền theo toạ độ ô cố định. Nếu ai đó thay biểu mẫu bằng bản khác bố cục,
/// chương trình vẫn chạy trơn tru nhưng số tiền rơi vào ô khác và bản in ra trống —
/// không có cách nào phát hiện lúc chạy. Bộ test này là lớp chặn duy nhất cho việc đó.</para>
/// </summary>
public class BhxhRemittanceTemplateShapeTests
{
    private static readonly IPaymentTemplateProvider Templates = new EmbeddedPaymentTemplateProvider();

    /// <summary>Toàn bộ ô mà handler ghi vào sheet dữ liệu.</summary>
    private static readonly string[] WrittenCells =
    [
        BhxhRemittanceFormCells.DocumentNo,
        BhxhRemittanceFormCells.DocumentDate,
        BhxhRemittanceFormCells.Description,
        BhxhRemittanceFormCells.AmountFigures,
        BhxhRemittanceFormCells.AmountWords,
        BhxhRemittanceFormCells.PayerName,
        BhxhRemittanceFormCells.PayerAccountNo,
        BhxhRemittanceFormCells.PayerBankName,
        BhxhRemittanceFormCells.BeneficiaryName,
        BhxhRemittanceFormCells.BeneficiaryAccountNo,
    ];

    private static WorksheetPart OpenSheet(SpreadsheetDocument doc, string name)
    {
        var workbookPart = doc.WorkbookPart!;
        var sheet = workbookPart.Workbook.Descendants<Sheet>()
            .FirstOrDefault(s => s.Name == name);

        sheet.Should().NotBeNull("biểu mẫu phải có sheet '{0}'", name);
        return (WorksheetPart)workbookPart.GetPartById(sheet!.Id!.Value!);
    }

    private static List<string> FormFormulas()
    {
        using var stream = Templates.Open(PaymentTemplateKind.BhxhRemittanceOrder);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;
        using var doc = SpreadsheetDocument.Open(ms, false);

        return OpenSheet(doc, BhxhRemittanceFormCells.FormSheet)
            .Worksheet.Descendants<Cell>()
            .Where(c => c.CellFormula is not null)
            .Select(c => c.CellFormula!.Text)
            .ToList();
    }

    [Fact]
    public void BieuMau_CoDuHaiSheet()
    {
        using var stream = Templates.Open(PaymentTemplateKind.BhxhRemittanceOrder);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;
        using var doc = SpreadsheetDocument.Open(ms, false);

        OpenSheet(doc, BhxhRemittanceFormCells.DataSheet).Should().NotBeNull();
        OpenSheet(doc, BhxhRemittanceFormCells.FormSheet).Should().NotBeNull();
    }

    [Fact]
    public void MoiOTaGhi_DeuDuocBanInThamChieuToi()
    {
        // Đây là khẳng định cốt lõi: ghi vào ô mà bản in không đọc tới = chứng từ trống.
        var formulas = FormFormulas();

        foreach (var cell in WrittenCells)
        {
            var absoluteRef = "$" + cell[..1] + "$" + cell[1..];   // "B24" -> "$B$24"

            formulas.Should().Contain(
                f => f.Contains($"{BhxhRemittanceFormCells.DataSheet}!{absoluteRef}"),
                "ô {0} phải được công thức trên sheet in tham chiếu tới", cell);
        }
    }

    [Fact]
    public void BanIn_CoDungHaiLienMoiLienMuoiOCongThuc()
    {
        // Mẫu DP004 in 2 liên (ngân hàng + khách hàng); thiếu liên nào là thiếu chứng từ nộp.
        var formulas = FormFormulas();

        formulas.Should().HaveCount(WrittenCells.Length * 2);
    }

    [Fact]
    public void BanIn_KhongThamChieuOLaNaoNgoaiDanhSachDaKhai()
    {
        // Nếu biểu mẫu đọc thêm ô mà handler không ghi, ô đó sẽ trống trên bản in.
        var referenced = FormFormulas()
            .SelectMany(f => Regex.Matches(f, $@"{BhxhRemittanceFormCells.DataSheet}!\$([A-Z]+)\$(\d+)"))
            .Select(m => m.Groups[1].Value + m.Groups[2].Value)
            .Distinct()
            .ToList();

        referenced.Should().BeEquivalentTo(WrittenCells);
    }

    [Fact]
    public void SheetDuLieu_KhongConSoLieuDemoCuaBieuMauGoc()
    {
        using var stream = Templates.Open(PaymentTemplateKind.BhxhRemittanceOrder);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;
        using var doc = SpreadsheetDocument.Open(ms, false);

        var sharedStrings = doc.WorkbookPart!.SharedStringTablePart?.SharedStringTable;
        var text = sharedStrings?.InnerText ?? string.Empty;

        // Biểu mẫu gốc tải về kèm số liệu mẫu của một công ty khác — phát hành kèm sản phẩm
        // thì chứng từ có thể in ra tên/tài khoản của đơn vị đó.
        text.Should().NotContain("HIM LAM");
        text.Should().NotContain("1020110000102717");
    }
}
