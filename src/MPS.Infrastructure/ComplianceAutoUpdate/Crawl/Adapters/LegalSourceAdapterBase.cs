using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Application.Options;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Crawl.Adapters;

/// <summary>
/// Base cho các adapter — gom phần dùng chung: HttpClient + UA giả browser + delay + fallback HTML rỗng.
/// </summary>
public abstract class LegalSourceAdapterBase : ILegalSourceAdapter
{
    private readonly HttpClient _http;
    private readonly ComplianceAutoUpdateOptions _options;
    protected readonly ILogger Logger;

    protected LegalSourceAdapterBase(HttpClient http, ComplianceAutoUpdateOptions options, ILogger logger)
    {
        _http = http;
        _options = options;
        Logger = logger;
    }

    public abstract string SourceKey { get; }

    public bool Enabled =>
        _options.Sources.TryGetValue(SourceKey, out var s) && s.Enabled;

    protected string BaseUrl =>
        _options.Sources.TryGetValue(SourceKey, out var s) ? s.BaseUrl : string.Empty;

    /// <summary>URL trang danh sách "văn bản mới" đã lọc lĩnh vực.</summary>
    protected abstract string ListUrl { get; }

    public virtual async Task<string> FetchListHtmlAsync(CancellationToken ct)
        => await GetHtmlAsync(ListUrl, ct);

    public virtual async Task<string> FetchFullTextAsync(string url, CancellationToken ct)
    {
        var html = await GetHtmlAsync(url, ct);
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var contentNode = SelectContentNode(doc) ?? doc.DocumentNode.SelectSingleNode("//body");
        var text = HtmlEntity.DeEntitize(contentNode?.InnerText ?? string.Empty);
        text = NormalizeWhitespace(text);

        // Fallback: trang render bằng JS → HtmlAgilityPack trả text rỗng.
        if (text.Length < 200)
        {
            Logger.LogWarning(
                "[{Source}] Toàn văn quá ngắn ({Len} ký tự) cho {Url} — nghi trang render JS. Trả rỗng.",
                SourceKey, text.Length, url);
            return string.Empty;
        }

        Logger.LogDebug("[{Source}] Tải toàn văn {Url}: {Len} ký tự.", SourceKey, url, text.Length);
        return text;
    }

    public abstract Task<IReadOnlyList<LegalDocumentRef>> FetchNewListAsync(CancellationToken ct);

    /// <summary>Override để chọn node thân văn bản đặc thù mỗi nguồn.</summary>
    protected virtual HtmlNode? SelectContentNode(HtmlDocument doc) => null;

    protected async Task<string> GetHtmlAsync(string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        try
        {
            // Delay lịch sự giữa request (vbpl/congbao nhạy cảm).
            await Task.Delay(_options.RequestDelayMs, ct);

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);
            req.Headers.TryAddWithoutValidation("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            req.Headers.TryAddWithoutValidation("Accept-Language", "vi-VN,vi;q=0.9");

            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                Logger.LogWarning("[{Source}] GET {Url} → HTTP {Code}.", SourceKey, url, (int)resp.StatusCode);
                return string.Empty;
            }
            return await resp.Content.ReadAsStringAsync(ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[{Source}] Lỗi tải {Url}.", SourceKey, url);
            return string.Empty;
        }
    }

    /// <summary>GET trả raw bytes (dùng tải PDF/DOCX toàn văn). Rỗng nếu lỗi.</summary>
    protected async Task<byte[]> GetBytesAsync(string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return Array.Empty<byte>();
        try
        {
            await Task.Delay(_options.RequestDelayMs, ct);
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                Logger.LogWarning("[{Source}] GET(bytes) {Url} → HTTP {Code}.", SourceKey, url, (int)resp.StatusCode);
                return Array.Empty<byte>();
            }
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[{Source}] Lỗi tải bytes {Url}.", SourceKey, url);
            return Array.Empty<byte>();
        }
    }

    /// <summary>Tải HTML một URL bất kỳ (cho adapter cần fetch trang phụ như sitemap/doc page).</summary>
    protected Task<string> FetchHtmlAsync(string url, CancellationToken ct) => GetHtmlAsync(url, ct);

    protected string ToAbsolute(string href)
    {
        if (string.IsNullOrWhiteSpace(href)) return string.Empty;
        if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return href;
        return Uri.TryCreate(new Uri(BaseUrl), href, out var abs) ? abs.ToString() : href;
    }

    protected static string NormalizeWhitespace(string s)
        => System.Text.RegularExpressions.Regex.Replace(s, @"[ \t]+", " ")
            .Replace("\r\n", "\n");
}
