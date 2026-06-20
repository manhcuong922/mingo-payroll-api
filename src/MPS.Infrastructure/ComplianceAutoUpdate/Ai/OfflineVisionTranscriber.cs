using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Ingestion;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Ai;

/// <summary>
/// Transcriber rỗng — dùng khi thiếu API key. OCR không khả dụng → trả rỗng (caller báo Failed).
/// Song song <see cref="OfflineComplianceExtractor"/>.
/// </summary>
public sealed class OfflineVisionTranscriber : IVisionTranscriber
{
    private readonly ILogger<OfflineVisionTranscriber> _logger;

    public OfflineVisionTranscriber(ILogger<OfflineVisionTranscriber> logger)
    {
        _logger = logger;
        _logger.LogWarning("Dùng OfflineVisionTranscriber (không LLM) — OCR PDF scan bị tắt do thiếu API key.");
    }

    public Task<string> TranscribePdfAsync(byte[] pdfBytes, CancellationToken ct) => Task.FromResult(string.Empty);
}
