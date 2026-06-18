using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Application.Options;
using MPS.Infrastructure.ComplianceAutoUpdate.Common;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Crawl.Adapters;

/// <summary>
/// Công báo điện tử (https://congbao.chinhphu.vn).
/// Danh sách "văn bản mới" nằm server-render trên trang chủ; toàn văn là PDF ký số (parse bằng PdfPig).
/// </summary>
public sealed class CongBaoAdapter : LegalSourceAdapterBase
{
    public const string Key = "congbao";
    private static readonly Regex DocNoRegex = new(@"\d{1,4}/\d{4}/[A-ZĐ\-]+", RegexOptions.Compiled);
    private static readonly Regex DocLinkRegex = new(@"^/van-ban/[a-z0-9\-]+-\d+\.htm$", RegexOptions.Compiled);
    private static readonly Regex SlugNoRegex = new(
        @"-so-(\d{1,4})-(\d{4})-([a-z]+(?:-[a-z]+)*)-\d+\.htm$", RegexOptions.Compiled);

    public CongBaoAdapter(HttpClient http, ComplianceAutoUpdateOptions options, ILogger<CongBaoAdapter> logger)
        : base(http, options, logger) { }

    public override string SourceKey => Key;

    // Trang chủ chứa danh sách văn bản mới đăng công báo (server-render).
    protected override string ListUrl => $"{BaseUrl}/";

    public override async Task<IReadOnlyList<LegalDocumentRef>> FetchNewListAsync(CancellationToken ct)
    {
        var html = await FetchListHtmlAsync(ct);
        if (string.IsNullOrWhiteSpace(html))
        {
            Logger.LogWarning("[{Source}] Trang chủ rỗng.", SourceKey);
            return Array.Empty<LegalDocumentRef>();
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
        var result = new List<LegalDocumentRef>();
        var seen = new HashSet<string>();
        if (anchors == null) return result;

        foreach (var a in anchors)
        {
            var href = a.GetAttributeValue("href", string.Empty);
            if (!DocLinkRegex.IsMatch(href)) continue;
            if (!seen.Add(href)) continue;

            var title = HtmlEntity.DeEntitize(a.InnerText).Trim();
            var docNo = ExtractDocNo(title, href);
            result.Add(new LegalDocumentRef
            {
                SourceKey = SourceKey,
                Url = ToAbsolute(href),
                Title = string.IsNullOrWhiteSpace(title) ? href : title,
                DocumentNo = docNo,
            });
        }

        Logger.LogInformation("[{Source}] Tìm thấy {Count} văn bản trên trang chủ.", SourceKey, result.Count);
        return result;
    }

    /// <summary>Toàn văn congbao = PDF ký số. Tải trang chi tiết → tìm link PDF → tải PDF → rút text.</summary>
    public override async Task<string> FetchFullTextAsync(string url, CancellationToken ct)
    {
        var page = await FetchHtmlAsync(url, ct);
        if (string.IsNullOrWhiteSpace(page))
        {
            Logger.LogWarning("[{Source}] Trang chi tiết rỗng: {Url}.", SourceKey, url);
            return string.Empty;
        }

        var pdfUrl = FindPdfUrl(page);
        if (pdfUrl == null)
        {
            Logger.LogWarning("[{Source}] Không thấy link PDF trong {Url}.", SourceKey, url);
            return string.Empty;
        }

        Logger.LogDebug("[{Source}] Tải PDF toàn văn: {Pdf}", SourceKey, pdfUrl);
        var bytes = await GetBytesAsync(pdfUrl, ct);
        if (bytes.Length == 0) return string.Empty;

        var text = NormalizeWhitespace(PdfTextExtractor.Extract(bytes));
        if (text.Length < 200)
        {
            Logger.LogWarning(
                "[{Source}] PDF rút được quá ít text ({Len}) — nghi scan ảnh: {Url}.", SourceKey, text.Length, url);
            return string.Empty;
        }
        Logger.LogDebug("[{Source}] Toàn văn từ PDF: {Len} ký tự.", SourceKey, text.Length);
        return text;
    }

    private static string? FindPdfUrl(string pageHtml)
    {
        // Ưu tiên file PDF ký số trên CDN công báo.
        var m = Regex.Match(pageHtml,
            @"https://congbaocdn\.chinhphu\.vn/[^""'\s]+\.pdf", RegexOptions.IgnoreCase);
        if (m.Success) return m.Value;

        // fallback: bất kỳ link .pdf nào.
        m = Regex.Match(pageHtml, @"https?://[^""'\s]+\.pdf", RegexOptions.IgnoreCase);
        return m.Success ? m.Value : null;
    }

    private static string? ExtractDocNo(string title, string href)
    {
        var m = DocNoRegex.Match(title);
        if (m.Success) return m.Value;

        var s = SlugNoRegex.Match(href);
        if (s.Success)
        {
            var no = s.Groups[1].Value;
            var year = s.Groups[2].Value;
            var kind = s.Groups[3].Value.ToUpperInvariant().Replace("-", "");
            return $"{no}/{year}/{kind}";
        }
        return null;
    }
}
