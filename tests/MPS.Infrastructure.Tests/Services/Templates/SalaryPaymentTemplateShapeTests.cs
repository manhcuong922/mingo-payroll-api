using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FluentAssertions;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Features.Payroll.PaymentDocuments.Common;
using MPS.Infrastructure.Services.Templates;
using Xunit;
using Cells = MPS.Application.Features.Payroll.PaymentDocuments.Common.SalaryPaymentFormCells;

namespace MPS.Infrastructure.Tests.Services.Templates;

/// <summary>
/// Chốt hình dạng biểu mẫu lệnh chi lương VPBank nhúng trong assembly.
///
/// <para>Code ghi theo toạ độ cố định và chèn dòng bằng cách dịch chỉ số. Cả hai đều giả định
/// một bố cục cụ thể. Nếu ai đó thay biểu mẫu bằng bản khác, chương trình vẫn chạy nhưng số tiền
/// rơi vào ô khác — bộ test này là lớp chặn duy nhất.</para>
/// </summary>
public class SalaryPaymentTemplateShapeTests
{
    private static readonly IPaymentTemplateProvider Templates = new EmbeddedPaymentTemplateProvider();

    private static (SpreadsheetDocument Doc, WorksheetPart Part) OpenTemplate(MemoryStream ms)
    {
        using (var stream = Templates.Open(PaymentTemplateKind.SalaryPaymentOrder))
        {
            stream.CopyTo(ms);
        }

        ms.Position = 0;
        var doc = SpreadsheetDocument.Open(ms, false);
        var workbookPart = doc.WorkbookPart!;
        var sheet = workbookPart.Workbook.Descendants<Sheet>()
            .FirstOrDefault(s => s.Name == Cells.Sheet);

        sheet.Should().NotBeNull("biểu mẫu phải có sheet '{0}'", Cells.Sheet);
        return (doc, (WorksheetPart)workbookPart.GetPartById(sheet!.Id!.Value!));
    }

    private static string? TextAt(SpreadsheetDocument doc, WorksheetPart part, string cellRef)
    {
        var cell = part.Worksheet.Descendants<Cell>()
            .FirstOrDefault(c => c.CellReference?.Value == cellRef);
        if (cell is null) return null;

        if (cell.DataType?.Value == CellValues.SharedString
            && int.TryParse(cell.CellValue?.InnerText, out var index))
        {
            return doc.WorkbookPart!.SharedStringTablePart!.SharedStringTable
                .Elements<SharedStringItem>().ElementAt(index).InnerText;
        }

        return cell.InlineString?.InnerText ?? cell.CellValue?.InnerText;
    }

    [Theory]
    [InlineData("A2", "ĐỀ NGHỊ THANH TOÁN LƯƠNG KIÊM ỦY NHIỆM CHI")]
    [InlineData("A29", "STT")]
    [InlineData("B29", "Họ và tên")]
    [InlineData("D29", "Số tiền")]
    [InlineData("A34", "STT")]
    [InlineData("G34", "Tên chi nhánh")]
    [InlineData("H34", "Tỉnh/ Thành phố")]
    [InlineData("A38", "Tổng cộng:")]
    public void BieuMau_GiuNguyenNhanODungChoCodeGiaDinh(string cell, string expected)
    {
        using var ms = new MemoryStream();
        var (doc, part) = OpenTemplate(ms);
        using var _ = doc;

        // Biểu mẫu có ô thụt đầu dòng bằng khoảng trắng — so sánh sau khi trim.
        TextAt(doc, part, cell)?.Trim().Should().Be(expected);
    }

    [Fact]
    public void BieuMau_MoiONhanCuaCodeDeuBatDauBangDungNhanDo()
    {
        // Ô chứa cả nhãn lẫn giá trị: ghi sai nhãn là chứng từ mất chữ "Số chứng từ*:".
        var fields = new[]
        {
            Cells.DocumentNo, Cells.IssueDate, Cells.Branch,
            Cells.CompanyName, Cells.CustomerCif, Cells.CompanyAddress, Cells.CompanyPhone,
            Cells.DebitAccount, Cells.Currency,
            Cells.TotalFigures, Cells.TotalWords, Cells.ValueDate, Cells.PaymentContent,
            Cells.TotalCountWords, Cells.TotalAmountWords,
        };

        using var ms = new MemoryStream();
        var (doc, part) = OpenTemplate(ms);
        using var _ = doc;

        foreach (var field in fields)
        {
            var actual = TextAt(doc, part, field.Cell);

            actual.Should().NotBeNull("ô {0} phải tồn tại trong biểu mẫu", field.Cell);
            actual!.TrimStart().Should().StartWith(field.Label.TrimEnd(),
                "hằng số phải trùng đúng chữ biểu mẫu đang in ở ô {0}", field.Cell);
        }
    }

    [Fact]
    public void BieuMau_ChuaSanDungBaDongDuLieuMoiBang()
    {
        // Hằng số PrebakedRowsPerTable quyết định khi nào phải chèn thêm dòng.
        (Cells.Table1LastPrebakedRow - Cells.Table1FirstDataRow + 1)
            .Should().Be(Cells.PrebakedRowsPerTable);
        (Cells.Table2LastPrebakedRow - Cells.Table2FirstDataRow + 1)
            .Should().Be(Cells.PrebakedRowsPerTable);
    }

    [Fact]
    public void BieuMau_DongDuLieuBang1CoVungGopFH()
    {
        // Bộ chèn dòng nhân bản vùng gộp này cho các dòng mới; mất nó là dòng mới lệch cột.
        using var ms = new MemoryStream();
        var (doc, part) = OpenTemplate(ms);
        using var _ = doc;

        var merges = part.Worksheet.Descendants<MergeCell>()
            .Select(m => m.Reference!.Value!).ToList();

        merges.Should().Contain($"F{Cells.Table1LastPrebakedRow}:H{Cells.Table1LastPrebakedRow}");
    }

    /// <summary>
    /// CANH GÁC: <see cref="XlsxRowInserter"/> chỉ biết dịch 8 loại cấu trúc đã liệt kê.
    /// Nếu biểu mẫu về sau có thêm loại khác cũng tham chiếu dòng, test này đỏ để buộc
    /// bổ sung code thay vì âm thầm cho ra chứng từ hỏng.
    /// </summary>
    [Fact]
    public void BieuMau_KhongChuaCauTrucThamChieuDongMaBoChenDongChuaXuLy()
    {
        using var ms = new MemoryStream();
        var (doc, part) = OpenTemplate(ms);
        using var _ = doc;

        var worksheet = part.Worksheet;

        worksheet.Descendants<DataValidation>().Should().BeEmpty(
            "dataValidation tham chiếu vùng ô — bộ chèn dòng chưa dịch loại này");
        worksheet.Descendants<ConditionalFormatting>().Should().BeEmpty(
            "conditionalFormatting tham chiếu vùng ô — bộ chèn dòng chưa dịch loại này");
        worksheet.Descendants<Hyperlink>().Should().BeEmpty(
            "hyperlink neo theo ô — bộ chèn dòng chưa dịch loại này");
        worksheet.Descendants<AutoFilter>().Should().BeEmpty(
            "autoFilter tham chiếu vùng ô — bộ chèn dòng chưa dịch loại này");
        worksheet.Descendants<TableParts>().Should().BeEmpty(
            "table tham chiếu vùng ô — bộ chèn dòng chưa dịch loại này");

        // Công thức cũng tham chiếu ô: mẫu này vốn không có công thức nào, nếu mọc thêm thì
        // bộ chèn dòng phải biết viết lại tham chiếu trong công thức.
        worksheet.Descendants<CellFormula>().Should().BeEmpty(
            "mẫu lệnh chi lương vốn không dùng công thức — bộ chèn dòng chưa viết lại tham chiếu");
    }
}
