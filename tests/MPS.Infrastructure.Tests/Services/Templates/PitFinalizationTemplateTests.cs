using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Features.Payroll.PaymentDocuments.Common;
using MPS.Infrastructure.Services;
using MPS.Infrastructure.Services.Templates;
using Xunit;

namespace MPS.Infrastructure.Tests.Services.Templates;

/// <summary>
/// Mẫu tờ khai quyết toán thuế TNCN 02/QTT-TNCN nhúng trong assembly.
///
/// <para>Rủi ro lớn nhất: một placeholder không được cấp giá trị sẽ in nguyên chữ
/// "{{k21}}" lên tờ khai mà NLĐ mang nộp cơ quan thuế.</para>
/// </summary>
public class PitFinalizationTemplateTests
{
    private static readonly IPaymentTemplateProvider Templates = new EmbeddedPaymentTemplateProvider();
    private static readonly DocxMergeService Renderer = new();

    private static readonly Regex PlaceholderPattern = new(@"\{\{([^}]+)\}\}", RegexOptions.Compiled);

    private static List<string> PlaceholdersIn(Stream docx)
    {
        using var ms = new MemoryStream();
        docx.CopyTo(ms);
        ms.Position = 0;

        using var doc = WordprocessingDocument.Open(ms, false);
        var body = doc.MainDocumentPart!.Document.Body!;

        return PlaceholderPattern.Matches(body.InnerText)
            .Select(m => m.Groups[1].Value.Trim())
            .Distinct()
            .ToList();
    }

    [Fact]
    public void BieuMau_CoDuBaMuoiChinOChiTieu()
    {
        using var template = Templates.Open(PaymentTemplateKind.PitFinalization);

        var placeholders = PlaceholdersIn(template);
        var indicatorKeys = PitFinalizationDocumentBuilder.IndicatorCodes
            .Select(c => "k" + c)
            .ToList();

        placeholders.Should().Contain(indicatorKeys,
            "mỗi chỉ tiêu [19]..[57] phải có một ô nhận giá trị");
        indicatorKeys.Should().HaveCount(39);
    }

    [Fact]
    public void BieuMau_CoDuOThongTinNguoiNopThueVaToChucTraThuNhap()
    {
        using var template = Templates.Open(PaymentTemplateKind.PitFinalization);

        PlaceholdersIn(template).Should().Contain(
        [
            "taxYear", "fromMonth", "toMonth",
            "taxpayerName", "taxpayerTaxCode", "taxpayerAddress", "taxpayerPhone", "taxpayerEmail",
            "employerName", "employerTaxCode", "employerAddress",
        ]);
    }

    [Fact]
    public async Task Render_KhongDeSotPlaceholderNaoTrenBanIn()
    {
        // Bộ giá trị TỐI THIỂU: mọi key mà biểu mẫu khai đều phải được builder cấp.
        using var probe = Templates.Open(PaymentTemplateKind.PitFinalization);
        var required = PlaceholdersIn(probe);

        var values = required.ToDictionary(key => key, _ => "X");

        using var template = Templates.Open(PaymentTemplateKind.PitFinalization);
        await using var rendered = await Renderer.RenderAsync(template, values);

        PlaceholdersIn(rendered).Should().BeEmpty(
            "placeholder còn sót sẽ in nguyên lên tờ khai nộp cơ quan thuế");
    }

    [Fact]
    public async Task Render_ODeTrongThiKhongConDauNgoac()
    {
        // Chỉ tiêu doanh nghiệp không nắm được cấp chuỗi rỗng — phải ra ô TRỐNG, không phải "{{k21}}".
        using var probe = Templates.Open(PaymentTemplateKind.PitFinalization);
        var values = PlaceholdersIn(probe).ToDictionary(key => key, _ => string.Empty);

        using var template = Templates.Open(PaymentTemplateKind.PitFinalization);
        await using var rendered = await Renderer.RenderAsync(template, values);

        using var ms = new MemoryStream();
        await rendered.CopyToAsync(ms);
        ms.Position = 0;

        using var doc = WordprocessingDocument.Open(ms, false);
        doc.MainDocumentPart!.Document.Body!.InnerText.Should().NotContain("{{");
    }

    [Fact]
    public async Task Render_GiuNguyenChuInSanCuaBieuMau()
    {
        using var probe = Templates.Open(PaymentTemplateKind.PitFinalization);
        var values = PlaceholdersIn(probe).ToDictionary(key => key, _ => "1.000.000");

        using var template = Templates.Open(PaymentTemplateKind.PitFinalization);
        await using var rendered = await Renderer.RenderAsync(template, values);

        using var ms = new MemoryStream();
        await rendered.CopyToAsync(ms);
        ms.Position = 0;

        using var doc = WordprocessingDocument.Open(ms, false);
        var text = doc.MainDocumentPart!.Document.Body!.InnerText;

        text.Should().Contain("TỜ KHAI QUYẾT TOÁN THUẾ THU NHẬP CÁ NHÂN");
        text.Should().Contain("Tổng thu nhập chịu thuế");
        text.Should().Contain("[37]");
        text.Should().Contain("1.000.000");
    }

    [Fact]
    public async Task Render_KhongSuaBieuMauGoc()
    {
        using (var first = Templates.Open(PaymentTemplateKind.PitFinalization))
        {
            await using var _ = await Renderer.RenderAsync(
                first, new Dictionary<string, string> { ["k19"] = "999" });
        }

        using var second = Templates.Open(PaymentTemplateKind.PitFinalization);
        PlaceholdersIn(second).Should().Contain("k19");
    }

    [Fact]
    public async Task Render_TraKeyKhongPhanBietHoaThuong()
    {
        using var template = Templates.Open(PaymentTemplateKind.PitFinalization);

        await using var rendered = await Renderer.RenderAsync(
            template, new Dictionary<string, string> { ["K19"] = "123.456" });

        using var ms = new MemoryStream();
        await rendered.CopyToAsync(ms);
        ms.Position = 0;

        using var doc = WordprocessingDocument.Open(ms, false);
        doc.MainDocumentPart!.Document.Body!.InnerText.Should().Contain("123.456");
    }
}
