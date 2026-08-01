using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Options;

namespace MPS.Infrastructure.Services;

/// <summary>
/// Gọi POST /reload_employees trên AI attendance service. Best-effort: mọi lỗi (network,
/// timeout, AiService:BaseUrl chưa cấu hình) chỉ được log, không throw — approve/reject
/// face registration không được phép fail vì AI service down.
/// </summary>
public sealed class FaceRecognitionSyncService : IFaceRecognitionSyncService
{
    private readonly HttpClient _http;
    private readonly AiServiceOptions _options;
    private readonly ILogger<FaceRecognitionSyncService> _logger;

    public FaceRecognitionSyncService(HttpClient http, IOptions<AiServiceOptions> options, ILogger<FaceRecognitionSyncService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _http.BaseAddress = new Uri(_options.BaseUrl);
        }
        _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task NotifyFaceRegistrationChangedAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogDebug("AiService:BaseUrl chưa cấu hình — bỏ qua reload_employees.");
            return;
        }

        try
        {
            var response = await _http.PostAsync("/reload_employees", content: null, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI service trả lỗi khi reload embeddings: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không gọi được AI service để reload embeddings.");
        }
    }
}
