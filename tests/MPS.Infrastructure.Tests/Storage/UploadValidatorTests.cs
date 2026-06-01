using System.Text;
using FluentAssertions;
using FluentValidation;
using MPS.Application.Common.Storage;

namespace MPS.Infrastructure.Tests.Storage;

public sealed class UploadValidatorTests
{
    private static readonly IReadOnlySet<string> PdfOnly =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/pdf" };

    [Fact]
    public async Task ValidateAsync_Passes_ForValidPdf()
    {
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }; // %PDF-
        using var ms = new MemoryStream(pdfBytes);

        var act = async () =>
            await UploadValidator.ValidateAsync(ms, "application/pdf", 1024, PdfOnly);

        await act.Should().NotThrowAsync();
        ms.Position.Should().Be(0, "validator must rewind the stream");
    }

    [Fact]
    public async Task ValidateAsync_Throws_WhenContentTypeNotAllowed()
    {
        using var ms = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        var act = async () =>
            await UploadValidator.ValidateAsync(ms, "image/png", 1024, PdfOnly);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*not allowed*");
    }

    [Fact]
    public async Task ValidateAsync_Throws_WhenFileTooLarge()
    {
        var payload = Encoding.ASCII.GetBytes("%PDF-1.4\n");
        using var ms = new MemoryStream(payload);

        var act = async () =>
            await UploadValidator.ValidateAsync(ms, "application/pdf", maxSizeBytes: 4, PdfOnly);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*exceeds the limit*");
    }

    [Fact]
    public async Task ValidateAsync_Throws_WhenMagicBytesMismatch()
    {
        var fakePdf = Encoding.ASCII.GetBytes("NOTPDF");
        using var ms = new MemoryStream(fakePdf);

        var act = async () =>
            await UploadValidator.ValidateAsync(ms, "application/pdf", 1024, PdfOnly);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*does not match*");
    }

    [Fact]
    public async Task ValidateAsync_SkipsMagicBytes_ForUnknownContentType()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "text/plain" };
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        var act = async () =>
            await UploadValidator.ValidateAsync(ms, "text/plain", 1024, allowed);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAsync_Throws_WhenStreamNotSeekable_AndMagicCheckRequired()
    {
        await using var nonSeekable = new NonSeekableStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        var act = async () =>
            await UploadValidator.ValidateAsync(nonSeekable, "application/pdf", 1024, PdfOnly);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*seekable*");
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner;
        public NonSeekableStream(byte[] data) => _inner = new MemoryStream(data);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
