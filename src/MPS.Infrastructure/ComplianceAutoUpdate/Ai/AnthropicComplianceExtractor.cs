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
/// </summary>
public sealed class AnthropicComplianceExtractor : IComplianceExtractor
{
    private readonly HttpClient _http;
    private readonly LlmOptions _llm;
    private readonly string _apiKey;
    private readonly int _maxInputChars;
    private readonly string _endpoint;
    private readonly ILogger<AnthropicComplianceExtractor> _logger;

    public AnthropicComplianceExtractor(
        HttpClient http, ComplianceAutoUpdateOptions options, ILogger<AnthropicComplianceExtractor> logger)
    {
        _http = http;
        _llm = options.Llm;
        _maxInputChars = options.Llm.MaxInputChars;
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

        var input = scopedText.Length > _maxInputChars ? scopedText[.._maxInputChars] : scopedText;

        var payload = new
        {
            model = _llm.Model,
            max_tokens = 2048,
            system = ExtractionPrompt.System,
            messages = new[]
            {
                new { role = "user", content = ExtractionPrompt.BuildUser(input) },
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
                _logger.LogError("Anthropic API lỗi HTTP {Code}: {Body}", (int)resp.StatusCode, Trunc(body));
                return null;
            }

            using var dom = JsonDocument.Parse(body);
            var text = dom.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;

            var result = JsonExtraction.TryParse(text);
            if (result == null)
                _logger.LogError("Không parse được JSON từ LLM. Đoạn thô: {Snippet}", Trunc(text));
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi gọi Anthropic API.");
            return null;
        }
    }

    private static string Trunc(string s) => s.Length <= 500 ? s : s[..500] + "…";
}
