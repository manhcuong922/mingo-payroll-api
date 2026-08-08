using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Ingestion;
using MPS.Application.Options;
using MPS.Infrastructure.ComplianceAutoUpdate.Common;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Intake;

/// <summary>
/// Kênh A — rút toàn văn từ file/URL người nạp. Tái dùng <see cref="PdfTextExtractor"/> cho PDF,
/// HtmlAgilityPack cho HTML (cùng cách LegalSourceAdapterBase). Dùng HttpClient "compliance-crawler".
/// </summary>
public sealed class DocumentTextFetcher : IDocumentTextFetcher
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IVisionTranscriber _vision;
    private readonly ComplianceAutoUpdateOptions _options;
    private readonly ILogger<DocumentTextFetcher> _logger;

    public DocumentTextFetcher(
        IHttpClientFactory httpFactory, IVisionTranscriber vision,
        ComplianceAutoUpdateOptions options, ILogger<DocumentTextFetcher> logger)
    {
        _httpFactory = httpFactory;
        _vision = vision;
        _options = options;
        _logger = logger;
    }

    public async Task<string> FromBytesAsync(byte[] bytes, string contentTypeOrName, CancellationToken ct)
    {
        if (bytes.Length == 0) return string.Empty;

        if (LooksLikePdf(bytes, contentTypeOrName))
        {
            var text = PdfTextExtractor.Extract(bytes);
            // PDF scan/ảnh: PdfPig bóc được rất ít text → fallback OCR bằng vision.
            if (_options.Ocr.Enabled && text.Length < _options.Ocr.MinPdfTextChars)
            {
                _logger.LogInformation(
                    "[Intake] PDF chỉ bóc được {Len} ký tự (< {Min}) — thử OCR bằng vision.",
                    text.Length, _options.Ocr.MinPdfTextChars);
                return await _vision.TranscribePdfAsync(bytes, ct);
            }
            return text;
        }

        // HTML hoặc text thuần — decode UTF-8 rồi strip nếu là HTML.
        var raw = System.Text.Encoding.UTF8.GetString(bytes);
        return LooksLikeHtml(contentTypeOrName, raw) ? HtmlToText(raw) : NormalizeWhitespace(raw);
    }

    public Task<string> FromUrlAsync(string url, CancellationToken ct) => FromUrlAsync(url, true, ct);

    /// <param name="followAttachment">
    /// Cho phép đi tiếp 1 nấc vào PDF đính kèm khi URL là trang giới thiệu văn bản. Chỉ true ở lần
    /// gọi đầu — chặn đệ quy vô hạn.
    /// </param>
    private async Task<string> FromUrlAsync(string url, bool followAttachment, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        try
        {
            var http = _httpFactory.CreateClient("compliance-crawler");
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);
            req.Headers.TryAddWithoutValidation("Accept-Language", "vi-VN,vi;q=0.9");

            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Intake] GET {Url} → HTTP {Code}.", url, (int)resp.StatusCode);
                return string.Empty;
            }

            var contentType = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            var hint = string.IsNullOrEmpty(contentType) ? url : contentType;
            var text = await FromBytesAsync(bytes, hint, ct);

            if (!followAttachment) return text;

            var raw = System.Text.Encoding.UTF8.GetString(bytes);
            if (!LooksLikeHtml(hint, raw) || HasArticleStructure(text)) return text;

            // Trang giới thiệu văn bản (vanban.chinhphu.vn, vbpl.vn...): phần strip HTML chỉ ra
            // menu/điều hướng, toàn văn nằm ở PDF đính kèm → đi tiếp 1 nấc vào file đó.
            var baseUri = resp.RequestMessage?.RequestUri ?? new Uri(url);
            var pdfUrl = FindAttachedPdfUrl(raw, baseUri);
            if (pdfUrl is null) return text;

            _logger.LogInformation("[Intake] {Url} là trang giới thiệu — lấy toàn văn từ {Pdf}.", url, pdfUrl);
            return await FromUrlAsync(pdfUrl, false, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Intake] Lỗi tải {Url}.", url);
            return string.Empty;
        }
    }

    /// <summary>
    /// Có ít nhất 2 mốc "Điều N." → coi là toàn văn. Cùng mốc nhận dạng với
    /// <c>ContentScopeFilter</c> nên khớp đúng thứ pipeline cần.
    /// </summary>
    private static bool HasArticleStructure(string text)
        => ArticleMarker.Matches(text).Count >= 2;

    private static readonly Regex ArticleMarker =
        new(@"Điều\s+\d+\s*\.", RegexOptions.Compiled);

    /// <summary>Link PDF đầu tiên trong trang (đã resolve tương đối → tuyệt đối).</summary>
    private static string? FindAttachedPdfUrl(string html, Uri baseUri)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var hrefs = doc.DocumentNode.SelectNodes("//a[@href]");
        if (hrefs is null) return null;

        foreach (var node in hrefs)
        {
            var href = HtmlEntity.DeEntitize(node.GetAttributeValue("href", string.Empty))?.Trim();
            if (string.IsNullOrEmpty(href)) continue;
            if (!Uri.TryCreate(baseUri, href, out var abs)) continue;
            if (abs.Scheme != Uri.UriSchemeHttp && abs.Scheme != Uri.UriSchemeHttps) continue;
            if (abs.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return abs.ToString();
        }
        return null;
    }

    private static bool LooksLikePdf(byte[] bytes, string hint)
    {
        if (hint.Contains("pdf", StringComparison.OrdinalIgnoreCase)) return true;
        // Magic bytes "%PDF"
        return bytes.Length >= 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46;
    }

    private static bool LooksLikeHtml(string hint, string raw)
    {
        if (hint.Contains("html", StringComparison.OrdinalIgnoreCase)) return true;
        if (hint.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) ||
            hint.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) return true;
        var head = raw.TrimStart();
        return head.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase)
            || head.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    private static string HtmlToText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var body = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
        var text = HtmlEntity.DeEntitize(body.InnerText ?? string.Empty);
        return NormalizeWhitespace(text);
    }

    private static string NormalizeWhitespace(string s)
        => Regex.Replace(s, @"[ \t]+", " ").Replace("\r\n", "\n");
}
