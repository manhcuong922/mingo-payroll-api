using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPS.Application.Features.Compliance.AutoUpdate.Ingestion;
using MPS.Application.Options;
using MPS.Infrastructure.ComplianceAutoUpdate.Intake;

namespace MPS.Infrastructure.Tests.ComplianceAutoUpdate;

/// <summary>
/// Test nhánh OCR fallback của DocumentTextFetcher: PDF scan/ảnh (PdfPig bóc rất ít text) →
/// gọi vision; PDF có đủ text hoặc OCR tắt → không gọi vision.
/// </summary>
public sealed class DocumentTextFetcherTests
{
    private readonly Mock<IVisionTranscriber> _vision = new();

    // Bytes mở đầu "%PDF" → LooksLikePdf=true; PdfPig không parse được → Extract trả rỗng (0 ký tự).
    private static readonly byte[] FakePdf = Encoding.ASCII.GetBytes("%PDF-1.4\nnot a real pdf body");

    private DocumentTextFetcher CreateSut(OcrOptions ocr) => new(
        Mock.Of<IHttpClientFactory>(),
        _vision.Object,
        new ComplianceAutoUpdateOptions { Ocr = ocr },
        NullLogger<DocumentTextFetcher>.Instance);

    [Fact]
    public async Task FromBytesAsync_ScannedPdf_OcrEnabled_CallsVision()
    {
        const string transcript = "Điều 3. Mức lương tối thiểu vùng I: 4.960.000 đồng";
        _vision.Setup(v => v.TranscribePdfAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcript);
        var sut = CreateSut(new OcrOptions { Enabled = true, MinPdfTextChars = 200 });

        var text = await sut.FromBytesAsync(FakePdf, "nghi-dinh.pdf", CancellationToken.None);

        text.Should().Be(transcript);
        _vision.Verify(v => v.TranscribePdfAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FromBytesAsync_ScannedPdf_OcrDisabled_DoesNotCallVision()
    {
        var sut = CreateSut(new OcrOptions { Enabled = false, MinPdfTextChars = 200 });

        var text = await sut.FromBytesAsync(FakePdf, "nghi-dinh.pdf", CancellationToken.None);

        text.Should().BeEmpty(); // PdfPig không bóc được, OCR tắt → rỗng (caller báo Failed)
        _vision.Verify(v => v.TranscribePdfAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FromBytesAsync_PdfMeetsTextThreshold_DoesNotCallVision()
    {
        // Ngưỡng 0 → text rỗng (0 ký tự) vẫn coi là "đủ" → không OCR (chứng minh nhánh false của gate).
        var sut = CreateSut(new OcrOptions { Enabled = true, MinPdfTextChars = 0 });

        await sut.FromBytesAsync(FakePdf, "nghi-dinh.pdf", CancellationToken.None);

        _vision.Verify(v => v.TranscribePdfAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FromBytesAsync_HtmlFile_StripsTextAndSkipsVision()
    {
        var html = Encoding.UTF8.GetBytes("<html><body><h1>Điều 3</h1><p>Vùng I: 4.960.000</p></body></html>");
        var sut = CreateSut(new OcrOptions { Enabled = true, MinPdfTextChars = 200 });

        var text = await sut.FromBytesAsync(html, "doc.html", CancellationToken.None);

        text.Should().Contain("4.960.000");
        text.Should().NotContain("<h1>");
        _vision.Verify(v => v.TranscribePdfAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
