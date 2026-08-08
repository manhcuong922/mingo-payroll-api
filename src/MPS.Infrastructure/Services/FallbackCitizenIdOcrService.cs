using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Options;
using MPS.Domain.Exceptions;

namespace MPS.Infrastructure.Services;

/// <summary>
/// Chạy Claude làm provider chính, rơi về FPT.AI khi Claude KHÔNG GỌI ĐƯỢC.
///
/// Chỉ fallback trên <see cref="OcrUnavailableException"/> (mất mạng, proxy chết, hết quota, thiếu key).
/// KHÔNG fallback khi Claude trả lời thành công nhưng phán ảnh sai loại
/// (<see cref="OcrInvalidDocumentTypeException"/>) hoặc không đọc nổi (<see cref="OcrFailedException"/>) —
/// đó là kết luận hợp lệ về tấm ảnh, hỏi lại provider khác chỉ tốn thời gian và dễ ra kết quả mâu thuẫn.
/// </summary>
public sealed class FallbackCitizenIdOcrService : ICitizenIdOcrService
{
    private readonly AnthropicCitizenIdOcrService _primary;
    private readonly FptAiCitizenIdOcrService _fallback;
    private readonly AnthropicOcrOptions _options;
    private readonly ILogger<FallbackCitizenIdOcrService> _logger;

    public FallbackCitizenIdOcrService(
        AnthropicCitizenIdOcrService primary,
        FptAiCitizenIdOcrService fallback,
        IOptions<AnthropicOcrOptions> options,
        ILogger<FallbackCitizenIdOcrService> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _options = options.Value;
        _logger = logger;
    }

    public Task<CitizenIdOcrResult> RecognizeAsync(Stream imageStream, string contentType, CancellationToken cancellationToken = default)
        => ExecuteAsync(imageStream, (svc, s) => svc.RecognizeAsync(s, contentType, cancellationToken), cancellationToken);

    public Task<CitizenIdBackOcrResult> RecognizeBackAsync(Stream imageStream, string contentType, CancellationToken cancellationToken = default)
        => ExecuteAsync(imageStream, (svc, s) => svc.RecognizeBackAsync(s, contentType, cancellationToken), cancellationToken);

    private async Task<T> ExecuteAsync<T>(
        Stream imageStream,
        Func<ICitizenIdOcrService, Stream, Task<T>> recognize,
        CancellationToken cancellationToken)
    {
        // Buffer một lần: mỗi provider cần một stream đọc từ đầu, không thể chia sẻ stream đã bị đọc cạn.
        using var buffer = new MemoryStream();
        await imageStream.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        if (!_primary.IsConfigured)
        {
            _logger.LogWarning("[OCR-CCCD] Claude chưa cấu hình — dùng thẳng FPT.AI.");
            return await recognize(_fallback, new MemoryStream(bytes));
        }

        try
        {
            return await recognize(_primary, new MemoryStream(bytes));
        }
        catch (OcrUnavailableException ex) when (_options.FallbackToFptAi && _fallback.IsConfigured)
        {
            _logger.LogWarning(ex, "[OCR-CCCD] Claude không khả dụng — thử lại bằng FPT.AI.");
            return await recognize(_fallback, new MemoryStream(bytes));
        }
    }
}
