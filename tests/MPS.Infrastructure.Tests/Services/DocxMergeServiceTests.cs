using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using MPS.Application.Common.Models.Document;
using MPS.Infrastructure.Services;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// Tests for DocxMergeService — verifies placeholder replacement, split-run normalization,
/// and preservation of unknown placeholders.
/// Fixtures are built in-memory using OpenXml API (no test assets required).
/// </summary>
public sealed class DocxMergeServiceTests
{
    private static PlaceholderContext BuildContext(string employeeFullName = "Nguyen Van A", string contractNo = "HDLD-CTXD-2026-0001")
    {
        return new PlaceholderContext(
            ContractNo: contractNo,
            ContractKind: "HĐLĐ",
            ContractCategory: "Xác định thời hạn",
            EffectiveFrom: "01/01/2026",
            EffectiveTo: "31/12/2027",
            SignedDate: "01/01/2026",
            SignedPlace: "Ha Noi",
            EmployerLegalName: "ACME Corp",
            EmployerTaxCode: "0123456789",
            EmployerAddress: "123 Main St",
            EmployerRepName: "Tran Van B",
            EmployerRepTitle: "Giam doc",
            EmployeeFullName: employeeFullName,
            EmployeeDob: "01/01/1990",
            EmployeeGender: "Nam",
            EmployeeCitizenId: "012345678901",
            EmployeeCitizenIssuedDate: "01/01/2020",
            EmployeeCitizenIssuedPlace: "Ha Noi",
            EmployeePermanentAddress: "456 Nguyen Trai",
            EmployeeContactAddress: "456 Nguyen Trai",
            PositionName: "Ky su phan mem",
            JobDescription: "Phat trien phan mem",
            WorkLocation: "Van phong Ha Noi",
            SalaryAmount: "20,000,000",
            SalaryCurrency: "VND",
            SalaryPayCycle: "Hàng tháng",
            Allowances: "Không có");
    }

    /// <summary>Creates a minimal valid DOCX stream with the given paragraph texts.</summary>
    private static Stream BuildDocx(params string[] paragraphTexts)
    {
        var ms = new MemoryStream();

        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, autoSave: false))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = new Body();
            main.Document.AppendChild(body);

            foreach (var text in paragraphTexts)
            {
                var para = new Paragraph();
                var run = new Run();
                run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
                para.AppendChild(run);
                body.AppendChild(para);
            }

            main.Document.Save();
        }

        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    /// <summary>Creates a DOCX where a placeholder is split across two runs (simulates Word spell-check fragmentation).</summary>
    private static Stream BuildDocxWithSplitPlaceholder()
    {
        var ms = new MemoryStream();

        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, autoSave: false))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = new Body();
            main.Document.AppendChild(body);

            var para = new Paragraph();

            var run1 = new Run();
            run1.AppendChild(new Text("{{employee") { Space = SpaceProcessingModeValues.Preserve });

            var run2 = new Run();
            run2.AppendChild(new Text("FullName}}") { Space = SpaceProcessingModeValues.Preserve });

            para.AppendChild(run1);
            para.AppendChild(run2);
            body.AppendChild(para);

            main.Document.Save();
        }

        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    private static string ExtractAllText(Stream docxStream)
    {
        docxStream.Seek(0, SeekOrigin.Begin);
        using var doc = WordprocessingDocument.Open(docxStream, isEditable: false);
        var body = doc.MainDocumentPart?.Document?.Body
            ?? throw new InvalidOperationException("Body missing");
        return string.Concat(body.Descendants<Text>().Select(t => t.Text));
    }

    [Fact]
    public async Task RenderAsync_Replaces_EmployeeFullName_Placeholder()
    {
        var template = BuildDocx("Tên NLĐ: {{employeeFullName}}");
        var ctx = BuildContext(employeeFullName: "Nguyen Van A");
        var svc = new DocxMergeService();

        var result = await svc.RenderAsync(template, ctx);

        var text = ExtractAllText(result);
        text.Should().Contain("Nguyen Van A");
        text.Should().NotContain("{{employeeFullName}}");
    }

    [Fact]
    public async Task RenderAsync_Replaces_ContractNo_Placeholder()
    {
        var template = BuildDocx("Mã HĐ: {{contractNo}}");
        var ctx = BuildContext(contractNo: "HDLD-CTXD-2026-0001");
        var svc = new DocxMergeService();

        var result = await svc.RenderAsync(template, ctx);

        var text = ExtractAllText(result);
        text.Should().Contain("HDLD-CTXD-2026-0001");
        text.Should().NotContain("{{contractNo}}");
    }

    [Fact]
    public async Task RenderAsync_Replaces_Multiple_Placeholders_In_Same_Paragraph()
    {
        var template = BuildDocx("{{employeeFullName}} - {{contractNo}}");
        var ctx = BuildContext("Le Thi C", "HDLD-TV-2026-0007");
        var svc = new DocxMergeService();

        var result = await svc.RenderAsync(template, ctx);

        var text = ExtractAllText(result);
        text.Should().Contain("Le Thi C");
        text.Should().Contain("HDLD-TV-2026-0007");
        text.Should().NotContain("{{");
    }

    [Fact]
    public async Task RenderAsync_Preserves_Unknown_Placeholders()
    {
        var template = BuildDocx("{{unknown.key}} is kept as-is");
        var ctx = BuildContext();
        var svc = new DocxMergeService();

        var result = await svc.RenderAsync(template, ctx);

        var text = ExtractAllText(result);
        text.Should().Contain("{{unknown.key}}");
    }

    [Fact]
    public async Task RenderAsync_Handles_Split_Run_Placeholder()
    {
        var template = BuildDocxWithSplitPlaceholder();
        var ctx = BuildContext(employeeFullName: "Pham Thi D");
        var svc = new DocxMergeService();

        var result = await svc.RenderAsync(template, ctx);

        var text = ExtractAllText(result);
        text.Should().Contain("Pham Thi D");
        text.Should().NotContain("{{employeeFullName}}");
    }

    [Fact]
    public async Task RenderAsync_Returns_Valid_Docx_Stream()
    {
        var template = BuildDocx("Hello {{employeeFullName}}");
        var ctx = BuildContext();
        var svc = new DocxMergeService();

        var result = await svc.RenderAsync(template, ctx);

        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
        result.CanRead.Should().BeTrue();

        // Should be a valid DOCX (ZIP signature PK)
        var header = new byte[4];
        await result.ReadAsync(header);
        header.Should().Equal([0x50, 0x4B, 0x03, 0x04]);
    }

    [Fact]
    public async Task RenderAsync_Replaces_All_Known_Placeholders_In_Separate_Paragraphs()
    {
        var template = BuildDocx(
            "{{employerLegalName}}",
            "{{employeeCitizenId}}",
            "{{salaryAmount}}",
            "{{positionName}}");
        var ctx = BuildContext();
        var svc = new DocxMergeService();

        var result = await svc.RenderAsync(template, ctx);

        var text = ExtractAllText(result);
        text.Should().Contain("ACME Corp");
        text.Should().Contain("012345678901");
        text.Should().Contain("20,000,000");
        text.Should().Contain("Ky su phan mem");
        text.Should().NotContain("{{");
    }
}
