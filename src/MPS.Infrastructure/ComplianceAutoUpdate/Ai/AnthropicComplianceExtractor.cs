using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Application.Options;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Ai;

/// <summary>
/// Gọi Anthropic Messages API để bóc số liệu.
/// API key đọc từ biến môi trường — KHÔNG hardcode. BaseUrl trống = gọi thẳng Anthropic;
/// set (vd CLIProxyAPI localhost:8317) thì ghép "/v1/messages".
///
/// Văn bản dài hơn <see cref="LlmOptions.MaxInputChars"/> được chia lô theo ranh giới "Điều N."
/// (xem <see cref="ExtractionBatcher"/>) rồi gộp kết quả — KHÔNG cắt cụt phần đuôi như trước.
/// </summary>
public sealed class AnthropicComplianceExtractor : IComplianceExtractor
{
    private const int MaxOutputTokens = 4096;

    private static readonly JsonSerializerOptions RawJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly HttpClient _http;
    private readonly LlmOptions _llm;
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly ILogger<AnthropicComplianceExtractor> _logger;

    public AnthropicComplianceExtractor(
        HttpClient http, ComplianceAutoUpdateOptions options, ILogger<AnthropicComplianceExtractor> logger)
    {
        _http = http;
        _llm = options.Llm;
        _apiKey = Environment.GetEnvironmentVariable(options.Llm.ApiKeyEnvVar) ?? string.Empty;
        var baseUrl = (options.Llm.BaseUrl ?? string.Empty).TrimEnd('/');
        _endpoint = string.IsNullOrEmpty(baseUrl)
            ? "https://api.anthropic.com/v1/messages"
            : $"{baseUrl}/v1/messages";
        _logger = logger;
    }

    public async Task<ExtractionResult?> ExtractAsync(string scopedText, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogError("Thiếu API key (biến môi trường {Var}) — không gọi được LLM.", _llm.ApiKeyEnvVar);
            return null;
        }

        var batches = ExtractionBatcher.Split(scopedText, _llm.MaxInputChars, _llm.MaxBatches);
        if (batches.Count == 0) return null;

        if (batches.Count > 1)
            _logger.LogInformation(
                "[Extract] Text {Len} ký tự > {Max} — chia {Count} lô theo điều khoản.",
                scopedText.Length, _llm.MaxInputChars, batches.Count);

        var parts = new List<ExtractionResult>(batches.Count);
        for (var i = 0; i < batches.Count; i++)
        {
            var part = await ExtractOneAsync(batches[i], i + 1, batches.Count, ct);
            if (part != null) parts.Add(part);
        }

        if (parts.Count == 0) return null;
        if (parts.Count == 1) return parts[0];   // giữ nguyên JSON thô model trả về (giá trị audit)

        // Nhiều lô: JSON thô của từng lô không còn đại diện cho kết quả cuối → dựng lại từ bản gộp.
        var merged = ExtractionBatcher.Merge(parts);
        merged.RawJson = JsonSerializer.Serialize(merged, RawJsonOptions);

        _logger.LogInformation(
            "[Extract] Gộp {Ok}/{Total} lô → {Count} field, entity={Entity}.",
            parts.Count, batches.Count, merged.Values.Count, merged.TargetEntityType ?? "(?)");

        return merged;
    }

    /// <summary>Một lượt gọi cho một lô. Lỗi HTTP/parse → null, caller bỏ lô đó và đi tiếp.</summary>
    private async Task<ExtractionResult?> ExtractOneAsync(string input, int part, int total, CancellationToken ct)
    {
        var payload = new
        {
            model = _llm.Model,
            max_tokens = MaxOutputTokens,
            system = ExtractionPrompt.System,
            messages = new[]
            {
                new { role = "user", content = ExtractionPrompt.BuildUser(input, part, total) },
            },
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint);
            req.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
            req.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
            req.Content = JsonContent.Create(payload);

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Anthropic API lỗi HTTP {Code} (lô {Part}/{Total}): {Body}",
                    (int)resp.StatusCode, part, total, Trunc(body));
                return null;
            }

            var text = AnthropicMessage.ReadText(body);
            var result = JsonExtraction.TryParse(text);
            if (result == null)
                _logger.LogError("Không parse được JSON từ LLM (lô {Part}/{Total}). Đoạn thô: {Snippet}",
                    part, total, Trunc(text));
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi gọi Anthropic API (lô {Part}/{Total}).", part, total);
            return null;
        }
    }

    private static string Trunc(string s) => s.Length <= 500 ? s : s[..500] + "…";
}
