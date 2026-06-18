using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Application.Options;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Crawl.Adapters;

/// <summary>
/// CSDL quốc gia về pháp luật (https://vbpl.vn). Trang đã chuyển sang SPA Next.js:
/// - DANH SÁCH: lấy qua sitemap.xml, lọc theo từ khoá lĩnh vực trong slug.
/// - TOÀN VĂN: trang chi tiết render bằng JS → KHÔNG lấy được bằng HTTP thuần (trả rỗng → mark Failed).
///   Bổ sung headless browser (PuppeteerSharp) sau nếu cần nguồn này.
/// </summary>
public sealed class VbplAdapter : LegalSourceAdapterBase
{
    public const string Key = "vbpl";
    private const int MaxCentralSitemaps = 2;   // giới hạn để không tải toàn bộ CSDL
    private const int MaxDocs = 60;

    private static readonly string[] SlugScopeKeywords =
    {
        "lao-dong", "tien-luong", "bao-hiem", "luong-toi-thieu", "thue-thu-nhap",
        "cong-doan", "lam-them", "phu-cap", "giam-tru", "muc-luong",
    };

    private static readonly Regex UrlEntry = new(
        @"<url>\s*<loc>(?<loc>.*?)</loc>\s*(?:<lastmod>(?<mod>.*?)</lastmod>)?",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex SlugDocNo = new(
        @"-so-(\d{1,4})-(\d{4})-([a-z]+(?:-[a-z]+)*?)--?\d+$", RegexOptions.Compiled);

    private List<LegalDocumentRef>? _cache;   // cache theo vòng đời scope (1 run)

    public VbplAdapter(HttpClient http, ComplianceAutoUpdateOptions options, ILogger<VbplAdapter> logger)
        : base(http, options, logger) { }

    public override string SourceKey => Key;

    protected override string ListUrl => $"{BaseUrl}/sitemap.xml";

    /// <summary>Cấp 1 cần "HTML danh sách" — dựng HTML tổng hợp từ sitemap để fingerprint hoạt động.</summary>
    public override async Task<string> FetchListHtmlAsync(CancellationToken ct)
    {
        var docs = await GetScopedDocsAsync(ct);
        var sb = new StringBuilder("<html><body>");
        foreach (var d in docs)
            sb.Append($"<a href=\"{d.Url}\">{System.Net.WebUtility.HtmlEncode(d.Title)}</a>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    public override async Task<IReadOnlyList<LegalDocumentRef>> FetchNewListAsync(CancellationToken ct)
    {
        var docs = await GetScopedDocsAsync(ct);
        Logger.LogInformation("[{Source}] {Count} văn bản trong scope (qua sitemap).", SourceKey, docs.Count);
        return docs;
    }

    /// <summary>Trang chi tiết là SPA → toàn văn không có trong HTML server. Trả rỗng (pipeline mark Failed).</summary>
    public override Task<string> FetchFullTextAsync(string url, CancellationToken ct)
    {
        Logger.LogWarning(
            "[{Source}] Toàn văn không lấy được qua HTTP (trang SPA, toàn văn nạp bằng JS): {Url}. " +
            "Bổ sung headless browser (PuppeteerSharp) nếu cần.", SourceKey, url);
        return Task.FromResult(string.Empty);
    }

    private async Task<List<LegalDocumentRef>> GetScopedDocsAsync(CancellationToken ct)
    {
        if (_cache != null) return _cache;

        var index = await FetchHtmlAsync(ListUrl, ct);
        var childSitemaps = Regex.Matches(index, @"<loc>(?<u>[^<]*sitemap-trung-uong-\d+\.xml)</loc>")
            .Select(m => m.Groups["u"].Value)
            .Take(MaxCentralSitemaps)
            .ToList();

        if (childSitemaps.Count == 0)
        {
            Logger.LogWarning("[{Source}] Sitemap index không có sitemap trung ương — nghi đổi cấu trúc.", SourceKey);
            _cache = new List<LegalDocumentRef>();
            return _cache;
        }

        var docs = new List<(LegalDocumentRef Ref, string Mod)>();
        foreach (var sm in childSitemaps)
        {
            var xml = await FetchHtmlAsync(sm, ct);
            foreach (Match m in UrlEntry.Matches(xml))
            {
                var loc = m.Groups["loc"].Value.Trim();
                if (string.IsNullOrEmpty(loc) || !loc.Contains("/van-ban/")) continue;

                var slug = loc.Split('/').Last();
                if (!SlugScopeKeywords.Any(k => slug.Contains(k, StringComparison.OrdinalIgnoreCase))) continue;

                docs.Add((new LegalDocumentRef
                {
                    SourceKey = SourceKey,
                    Url = loc,
                    Title = SlugToTitle(slug),
                    DocumentNo = DocNoFromSlug(slug),
                }, m.Groups["mod"].Value.Trim()));
            }
        }

        _cache = docs
            .OrderByDescending(d => d.Mod, StringComparer.Ordinal)   // lastmod yyyy-MM-dd → sort chuỗi = sort ngày
            .Select(d => d.Ref)
            .Take(MaxDocs)
            .ToList();

        Logger.LogInformation(
            "[{Source}] Quét {N} sitemap trung ương → {M} văn bản trong scope.", SourceKey, childSitemaps.Count, _cache.Count);
        return _cache;
    }

    private static string SlugToTitle(string slug)
    {
        var s = Regex.Replace(slug, @"--?\d+$", "");      // bỏ ItemID đuôi
        s = s.Replace('-', ' ');
        return CultureTitle(s);
    }

    private static string CultureTitle(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string? DocNoFromSlug(string slug)
    {
        var m = SlugDocNo.Match(slug);
        if (!m.Success) return null;
        var no = m.Groups[1].Value;
        var year = m.Groups[2].Value;
        var kind = m.Groups[3].Value.ToUpperInvariant().Replace("-", "");
        return $"{no}/{year}/{kind}";
    }
}
