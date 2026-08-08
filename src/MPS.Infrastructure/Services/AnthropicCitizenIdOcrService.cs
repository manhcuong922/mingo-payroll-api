using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Options;
using MPS.Domain.Exceptions;
using Polly;
using Polly.Retry;

namespace MPS.Infrastructure.Services;

/// <summary>
/// OCR CCCD bằng Claude vision (Anthropic Messages API), ép định dạng bằng structured outputs.
///
/// Trả về đúng <see cref="CitizenIdOcrResult"/>/<see cref="CitizenIdBackOcrResult"/> như FPT.AI:
/// model được ép sinh JSON trùng tên field của FPT, rồi map qua <see cref="CitizenIdOcrParsing"/> dùng chung.
/// </summary>
public sealed class AnthropicCitizenIdOcrService : ICitizenIdOcrService
{
    private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";

    private readonly HttpClient _http;
    private readonly AnthropicOcrOptions _options;
    private readonly ILogger<AnthropicCitizenIdOcrService> _logger;
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public AnthropicCitizenIdOcrService(
        HttpClient http,
        IOptions<AnthropicOcrOptions> options,
        ILogger<AnthropicCitizenIdOcrService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _apiKey = Environment.GetEnvironmentVariable(_options.ApiKeyEnvVar) ?? string.Empty;

        var baseUrl = (_options.BaseUrl ?? string.Empty).TrimEnd('/');
        _endpoint = string.IsNullOrEmpty(baseUrl) ? AnthropicApiUrl : $"{baseUrl}/v1/messages";
        _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        _retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => (int)r.StatusCode >= 500 || (int)r.StatusCode == 429)
            .WaitAndRetryAsync(2, attempt => TimeSpan.FromSeconds(attempt));
    }

    /// <summary>Provider chỉ dùng được khi bật và có API key — dùng để quyết định fallback sang FPT.AI.</summary>
    public bool IsConfigured => _options.Enabled && !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<CitizenIdOcrResult> RecognizeAsync(Stream imageStream, string contentType, CancellationToken cancellationToken = default)
    {
        var json = await CallClaudeAsync(
            imageStream, contentType, CitizenIdOcrPrompt.FrontSystem, CitizenIdOcrPrompt.FrontSchema(), cancellationToken);
        using var doc = ParseJsonPayload(json);
        return CitizenIdOcrParsing.ParseFront(doc.RootElement);
    }

    public async Task<CitizenIdBackOcrResult> RecognizeBackAsync(Stream imageStream, string contentType, CancellationToken cancellationToken = default)
    {
        var json = await CallClaudeAsync(
            imageStream, contentType, CitizenIdOcrPrompt.BackSystem, CitizenIdOcrPrompt.BackSchema(), cancellationToken);
        using var doc = ParseJsonPayload(json);
        return CitizenIdOcrParsing.ParseBack(doc.RootElement);
    }

    /// <summary>Gọi Messages API, trả về phần text (kỳ vọng là JSON) trong content block đầu tiên.</summary>
    private async Task<string> CallClaudeAsync(
        Stream imageStream, string contentType, string systemPrompt, object schema, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
            throw new OcrUnavailableException(new InvalidOperationException(
                $"Anthropic OCR chưa cấu hình (Enabled={_options.Enabled}, biến môi trường {_options.ApiKeyEnvVar} rỗng)."));

        // Buffer trước để Polly retry dựng lại được body ở lần thử sau.
        var base64 = await ToBase64Async(imageStream, cancellationToken);

        var payload = new
        {
            model = _options.Model,
            max_tokens = _options.MaxOutputTokens,
            system = systemPrompt,
            output_config = new
            {
                effort = _options.Effort,
                format = new { type = "json_schema", schema },
            },
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "image", source = new { type = "base64", media_type = contentType, data = base64 } },
                        new { type = "text", text = CitizenIdOcrPrompt.User },
                    },
                },
            },
        };

        HttpResponseMessage response;
        try
        {
            response = await _retryPolicy.ExecuteAsync(async () =>
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint);
                req.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
                req.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
                req.Content = JsonContent.Create(payload);
                return await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, cancellationToken);
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            throw new OcrUnavailableException(ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[OCR-CCCD] Anthropic trả HTTP {Code}: {Body}", (int)response.StatusCode, Trunc(body));
                throw new OcrUnavailableException(new HttpRequestException($"HTTP {(int)response.StatusCode}"));
            }

            return ExtractText(body);
        }
    }

    /// <summary>Bóc text từ response Messages API, xử lý cả trường hợp model từ chối trả lời.</summary>
    private string ExtractText(string body)
    {
        JsonDocument dom;
        try
        {
            dom = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[OCR-CCCD] Response không phải JSON: {Body}", Trunc(body));
            throw new OcrUnavailableException(ex);
        }

        using (dom)
        {
            var root = dom.RootElement;

            // Safety classifier từ chối → HTTP 200 nhưng stop_reason = "refusal", content có thể rỗng.
            // Phải kiểm tra TRƯỚC khi đọc content[0], nếu không sẽ ném IndexOutOfRange.
            if (root.TryGetProperty("stop_reason", out var stop) && stop.GetString() == "refusal")
            {
                var category = root.TryGetProperty("stop_details", out var d) && d.ValueKind == JsonValueKind.Object
                    ? d.TryGetProperty("category", out var c) ? c.GetString() : null
                    : null;
                _logger.LogWarning("[OCR-CCCD] Model từ chối xử lý ảnh (category={Category}).", category ?? "n/a");
                throw new OcrFailedException($"Model từ chối xử lý ảnh này (category={category ?? "n/a"}).");
            }

            if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                throw new OcrFailedException("Response thiếu mảng content.");

            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var t) && t.GetString() == "text"
                    && block.TryGetProperty("text", out var text)
                    && text.GetString() is { Length: > 0 } value)
                {
                    return value;
                }
            }

            _logger.LogError("[OCR-CCCD] Không có text block nào trong response: {Body}", Trunc(body));
            throw new OcrFailedException("Model không trả về nội dung nào.");
        }
    }

    /// <summary>
    /// Parse JSON model trả về. Structured outputs đảm bảo JSON sạch, nhưng nếu proxy nuốt mất
    /// output_config thì model có thể bọc trong ```json — nên bóc rào trước khi parse.
    /// </summary>
    private JsonDocument ParseJsonPayload(string raw)
    {
        var text = StripCodeFence(raw.Trim());
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[OCR-CCCD] Không parse được JSON model trả về: {Text}", Trunc(text));
            throw new OcrFailedException("Model trả về dữ liệu không đúng định dạng JSON.");
        }
    }

    private static string StripCodeFence(string s)
    {
        if (!s.StartsWith("```", StringComparison.Ordinal)) return s;

        var firstNewline = s.IndexOf('\n');
        if (firstNewline < 0) return s;

        var closing = s.LastIndexOf("```", StringComparison.Ordinal);
        var end = closing > firstNewline ? closing : s.Length;
        return s[(firstNewline + 1)..end].Trim();
    }

    private static async Task<string> ToBase64Async(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static string Trunc(string s) => s.Length <= 500 ? s : s[..500] + "…";
}
