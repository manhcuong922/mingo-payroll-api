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

    public async Task<string> FromUrlAsync(string url, CancellationToken ct)
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
            return await FromBytesAsync(bytes, string.IsNullOrEmpty(contentType) ? url : contentType, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Intake] Lỗi tải {Url}.", url);
            return string.Empty;
        }
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
