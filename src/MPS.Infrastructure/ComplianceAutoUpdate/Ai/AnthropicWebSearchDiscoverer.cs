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
            max_tokens = _watcher.MaxOutputTokens,
            tools = new object[] { BuildWebSearchTool() },
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

        // CHỐT AN TOÀN: không có lượt search nào chạy thì câu trả lời chỉ là trí nhớ của model.
        // Bỏ chốt này là rước hỏng câu: hasNewer=false bịa ra sẽ thành NoChange → ReVerifyAsync
        // đóng dấu "đã kiểm chứng" lên luật đã lỗi thời, sai âm thầm và không ai biết.
        // Đúng tình huống gặp thật khi web_search_20260209 chạy qua proxy: HTTP 200, JSON hợp lệ,
        // web_search_requests = 0.
        var searches = ReadWebSearchRequests(body);
        if (searches == 0)
            throw new WebSearchUnavailableException(
                "Model không thực hiện lượt web_search nào — câu trả lời không có căn cứ, bỏ qua. " +
                $"Kiểm tra ComplianceAutoUpdate:Watcher:WebSearchToolType (đang là '{_watcher.WebSearchToolType}'); " +
                "bản web_search_20260209 cần code execution nên không chạy được qua CLIProxyAPI.");

        var text = ExtractText(body);
        var parsed = TryParse(text);
        if (parsed is null)
        {
            // Chạm trần max_tokens → JSON bị cắt giữa chừng; báo rõ để chỉnh Watcher:MaxOutputTokens
            // thay vì đổ lỗi cho model.
            var stop = ReadStopReason(body);
            var reason = stop == "max_tokens"
                ? $"trả lời bị cắt do chạm max_tokens ({_watcher.MaxOutputTokens}) — tăng ComplianceAutoUpdate:Watcher:MaxOutputTokens"
                : $"stop_reason={stop ?? "(?)"}";
            _logger.LogWarning("[Watcher] {Entity}: parse JSON thất bại ({Reason}). Text: {Text}",
                targetEntityType, reason, Trunc(text));
            throw new WebSearchUnavailableException($"Không parse được JSON từ web_search — {reason}.");
        }

        if (!parsed.HasNewer)
        {
            _logger.LogInformation("[Watcher] {Entity}: không có bản mới hơn.", targetEntityType);
            return null;
        }

        _logger.LogInformation("[Watcher] {Entity}: nghi có bản mới — docNo={Doc}, url={Url}.",
            targetEntityType, parsed.DocumentNo, parsed.SourceUrl);
        return new DiscoveryResult(parsed.DocumentNo, parsed.EffectiveDate, parsed.SourceUrl, parsed.Summary ?? string.Empty);
    }

    /// <summary>
    /// Tool web_search. Có <c>AllowedDomains</c> thì kèm <c>allowed_domains</c> để model chỉ tra
    /// trang pháp luật chính thống — tránh trả URL bài báo (không có "Điều N." → pipeline lọc ra 0 điều).
    /// </summary>
    private object BuildWebSearchTool()
    {
        // Distinct: Anthropic trả 400 nếu allowed_domains có phần tử trùng, mà ConfigurationBinder
        // của .NET lại NỐI THÊM giá trị config vào mảng mặc định — rất dễ sinh trùng khi override.
        var domains = (_watcher.AllowedDomains ?? Array.Empty<string>())
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var type = string.IsNullOrWhiteSpace(_watcher.WebSearchToolType)
            ? "web_search_20250305"
            : _watcher.WebSearchToolType.Trim();

        if (domains.Length == 0)
            return new { type, name = "web_search", max_uses = _watcher.MaxSearchUses };

        return new
        {
            type,
            name = "web_search",
            max_uses = _watcher.MaxSearchUses,
            allowed_domains = domains,
        };
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

    /// <summary>
    /// Số lượt web_search thực sự chạy (<c>usage.server_tool_use.web_search_requests</c>).
    /// Thiếu field → trả -1 (không kết luận được, coi như đã search để không chặn oan).
    /// </summary>
    private static int ReadWebSearchRequests(string body)
    {
        try
        {
            using var dom = JsonDocument.Parse(body);
            if (dom.RootElement.TryGetProperty("usage", out var usage)
                && usage.TryGetProperty("server_tool_use", out var stu)
                && stu.TryGetProperty("web_search_requests", out var n)
                && n.TryGetInt32(out var value))
                return value;
            return -1;
        }
        catch (JsonException) { return -1; }
    }

    /// <summary>Đọc stop_reason để phân biệt "model trả sai format" với "bị cắt vì hết token".</summary>
    private static string? ReadStopReason(string body)
    {
        try
        {
            using var dom = JsonDocument.Parse(body);
            return dom.RootElement.TryGetProperty("stop_reason", out var s) ? s.GetString() : null;
        }
        catch (JsonException) { return null; }
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
