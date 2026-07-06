using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Ingestion;
using MPS.Application.Options;
using UglyToad.PdfPig;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Ai;

/// <summary>
/// Phiên âm PDF scan/ảnh qua Anthropic Messages API (document block + vision).
/// Mirror <see cref="AnthropicComplianceExtractor"/>: key từ biến môi trường, BaseUrl rỗng = gọi thẳng Anthropic.
/// </summary>
public sealed class AnthropicVisionTranscriber : IVisionTranscriber
{
    private readonly HttpClient _http;
    private readonly LlmOptions _llm;
    private readonly OcrOptions _ocr;
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly ILogger<AnthropicVisionTranscriber> _logger;

    public AnthropicVisionTranscriber(
        HttpClient http, ComplianceAutoUpdateOptions options, ILogger<AnthropicVisionTranscriber> logger)
    {
        _http = http;
        _llm = options.Llm;
        _ocr = options.Ocr;
        _apiKey = Environment.GetEnvironmentVariable(options.Llm.ApiKeyEnvVar) ?? string.Empty;
        var baseUrl = (options.Llm.BaseUrl ?? string.Empty).TrimEnd('/');
        _endpoint = string.IsNullOrEmpty(baseUrl)
            ? "https://api.anthropic.com/v1/messages"
            : $"{baseUrl}/v1/messages";
        _logger = logger;
    }

    public async Task<string> TranscribePdfAsync(byte[] pdfBytes, CancellationToken ct)
    {
        if (pdfBytes.Length == 0) return string.Empty;
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogError("Thiếu API key (biến môi trường {Var}) — không OCR được.", _llm.ApiKeyEnvVar);
            return string.Empty;
        }

        // Đếm trang để chặn file quá lớn (kiểm soát token). Lỗi đọc trang → coi như không OCR được.
        int pages;
        try
        {
            using var doc = PdfDocument.Open(pdfBytes);
            pages = doc.NumberOfPages;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OCR] Không mở được PDF để đếm trang — bỏ qua.");
            return string.Empty;
        }

        if (pages > _ocr.MaxPdfPages)
        {
            _logger.LogWarning("[OCR] PDF {Pages} trang > cap {Cap} — bỏ qua để tránh tốn token.", pages, _ocr.MaxPdfPages);
            return string.Empty;
        }

        var base64 = Convert.ToBase64String(pdfBytes);
        // Model OCR riêng (rẻ hơn) — rỗng thì fallback về model chung của LLM.
        var model = string.IsNullOrWhiteSpace(_ocr.Model) ? _llm.Model : _ocr.Model;
        var payload = new
        {
            model,
            max_tokens = _ocr.MaxOutputTokens,
            system = TranscriptionPrompt.System,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "document", source = new { type = "base64", media_type = "application/pdf", data = base64 } },
                        new { type = "text", text = TranscriptionPrompt.User },
                    },
                },
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
                _logger.LogError("[OCR] Anthropic API lỗi HTTP {Code}: {Body}", (int)resp.StatusCode, Trunc(body));
                return string.Empty;
            }

            using var dom = JsonDocument.Parse(body);
            var text = dom.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
            _logger.LogInformation("[OCR] Phiên âm {Pages} trang → {Len} ký tự.", pages, text.Length);
            return text;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR] Lỗi gọi Anthropic API.");
            return string.Empty;
        }
    }

    private static string Trunc(string s) => s.Length <= 500 ? s : s[..500] + "…";
}
