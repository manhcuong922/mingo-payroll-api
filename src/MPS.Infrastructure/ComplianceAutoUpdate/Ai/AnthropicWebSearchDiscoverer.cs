using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Application.Features.Compliance.AutoUpdate.Watcher;
using MPS.Application.Options;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Ai;

/// <summary>
/// Kênh B — gọi Anthropic Messages API kèm server tool <c>web_search</c> để tìm văn bản mới hơn.
/// Mirror <see cref="AnthropicComplianceExtractor"/>. Lỗi gọi / parse → ném
/// <see cref="WebSearchUnavailableException"/> để watcher degrade sang NeedsHuman.
/// </summary>
public sealed class AnthropicWebSearchDiscoverer : IWebSearchDiscoverer
{
    private readonly HttpClient _http;
    private readonly LlmOptions _llm;
    private readonly WatcherOptions _watcher;
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly ILogger<AnthropicWebSearchDiscoverer> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AnthropicWebSearchDiscoverer(
        HttpClient http, ComplianceAutoUpdateOptions options, ILogger<AnthropicWebSearchDiscoverer> logger)
    {
        _http = http;
        _llm = options.Llm;
        _watcher = options.Watcher;
        _apiKey = Environment.GetEnvironmentVariable(options.Llm.ApiKeyEnvVar) ?? string.Empty;
        var baseUrl = (options.Llm.BaseUrl ?? string.Empty).TrimEnd('/');
        _endpoint = string.IsNullOrEmpty(baseUrl)
            ? "https://api.anthropic.com/v1/messages"
            : $"{baseUrl}/v1/messages";
        _logger = logger;
    }

    public async Task<DiscoveryResult?> DiscoverNewerAsync(
        string targetEntityType, string? currentDocumentNo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new WebSearchUnavailableException($"Thiếu API key ({_llm.ApiKeyEnvVar}).");

        var payload = new
        {
            // Model rẻ hơn cho watcher (rỗng → dùng Llm.Model). max_uses chặn số lượt search/loại.
            model = string.IsNullOrWhiteSpace(_watcher.Model) ? _llm.Model : _watcher.Model,
            max_tokens = 2048,
            tools = new object[] { new { type = "web_search_20260209", name = "web_search", max_uses = _watcher.MaxSearchUses } },
            system = WatcherPrompt.System,
            messages = new[]
            {
                new { role = "user", content = WatcherPrompt.BuildUser(targetEntityType, currentDocumentNo) },
            },
        };

        string body;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint);
            req.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
            req.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
            req.Content = JsonContent.Create(payload);

            using var resp = await _http.SendAsync(req, ct);
            body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new WebSearchUnavailableException(
                    $"web_search HTTP {(int)resp.StatusCode}: {Trunc(body)}");
        }
        catch (WebSearchUnavailableException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new WebSearchUnavailableException($"Lỗi gọi web_search: {ex.Message}");
        }

        var text = ExtractText(body);
        var parsed = TryParse(text);
        if (parsed is null)
            throw new WebSearchUnavailableException("Không parse được JSON từ web_search.");

        if (!parsed.HasNewer)
        {
            _logger.LogInformation("[Watcher] {Entity}: không có bản mới hơn.", targetEntityType);
            return null;
        }

        _logger.LogInformation("[Watcher] {Entity}: nghi có bản mới — docNo={Doc}, url={Url}.",
            targetEntityType, parsed.DocumentNo, parsed.SourceUrl);
        return new DiscoveryResult(parsed.DocumentNo, parsed.EffectiveDate, parsed.SourceUrl, parsed.Summary ?? string.Empty);
    }

    /// <summary>Gộp các block "text" trong content (bỏ qua server_tool_use/web_search_tool_result).</summary>
    private static string ExtractText(string body)
    {
        try
        {
            using var dom = JsonDocument.Parse(body);
            if (!dom.RootElement.TryGetProperty("content", out var content)) return string.Empty;
            var sb = new StringBuilder();
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var t) && t.GetString() == "text"
                    && block.TryGetProperty("text", out var txt))
                    sb.AppendLine(txt.GetString());
            }
            return sb.ToString();
        }
        catch (JsonException) { return string.Empty; }
    }

    private static DiscoveryDto? TryParse(string text)
    {
        var json = JsonExtraction.StripFences(text);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<DiscoveryDto>(json, JsonOpts); }
        catch (JsonException) { return null; }
    }

    private static string Trunc(string s) => s.Length <= 500 ? s : s[..500] + "…";

    private sealed class DiscoveryDto
    {
        public bool HasNewer { get; set; }
        public string? DocumentNo { get; set; }

        [JsonConverter(typeof(NullableDateOnlyConverter))]
        public DateOnly? EffectiveDate { get; set; }

        public string? SourceUrl { get; set; }
        public string? Summary { get; set; }
    }
}
