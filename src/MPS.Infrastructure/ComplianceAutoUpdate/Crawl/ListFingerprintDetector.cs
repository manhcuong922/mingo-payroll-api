using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Compliance.AutoUpdate.Abstractions;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Infrastructure.ComplianceAutoUpdate.Common;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Crawl;

/// <summary>
/// Cấp 1 — fingerprint cả danh sách.
/// CHỈ hash phần ổn định (tiêu đề + số hiệu), bỏ qua ngày giờ/view count/banner/token.
/// </summary>
public sealed class ListFingerprintDetector : IListFingerprintDetector
{
    private readonly AppDbContext _db;
    private readonly ILogger<ListFingerprintDetector> _logger;

    // Số hiệu văn bản VN: 74/2024/NĐ-CP, 293/2025/NĐ-CP, 26/2016/TT-BLĐTBXH...
    private static readonly Regex DocNoRegex =
        new(@"\d{1,4}/\d{4}/[A-ZĐ\-]+", RegexOptions.Compiled);

    public ListFingerprintDetector(AppDbContext db, ILogger<ListFingerprintDetector> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string ExtractStableContent(string listHtml)
    {
        if (string.IsNullOrWhiteSpace(listHtml)) return string.Empty;

        var doc = new HtmlDocument();
        doc.LoadHtml(listHtml);

        var nodes = doc.DocumentNode.SelectNodes(
            "//a[@href] | //*[self::h2 or self::h3 or self::h4]");

        var items = new List<string>();
        if (nodes != null)
        {
            foreach (var n in nodes)
            {
                var text = HtmlEntity.DeEntitize(n.InnerText ?? string.Empty).Trim();
                text = Regex.Replace(text, @"\s+", " ");
                if (text.Length < 8) continue;

                var m = DocNoRegex.Match(text);
                var key = m.Success ? $"{m.Value} | {text}" : text;
                items.Add(key);
            }
        }

        var stable = items.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        var sb = new StringBuilder();
        foreach (var s in stable) sb.Append(s).Append('\n');
        return sb.ToString();
    }

    public async Task<bool> HasChangedAsync(string sourceKey, string listHtml, CancellationToken ct)
    {
        var stable = ExtractStableContent(listHtml);
        if (string.IsNullOrWhiteSpace(stable))
        {
            // Dấu hiệu trang đổi layout / selector hỏng — KHÔNG để hỏng âm thầm.
            _logger.LogWarning(
                "[Cấp 1] Nguồn {Source}: ExtractStableContent rỗng — nghi selector hỏng/đổi layout.",
                sourceKey);
            return false;
        }

        var fingerprint = Hashing.Sha256Hex(stable);
        var snapshot = await _db.ComplianceSourceSnapshots
            .FirstOrDefaultAsync(s => s.SourceKey == sourceKey, ct);

        var old = snapshot?.Fingerprint ?? "(none)";
        if (snapshot != null && snapshot.Fingerprint == fingerprint)
        {
            _logger.LogInformation(
                "[Cấp 1] Nguồn {Source}: fingerprint không đổi ({Fp}) — DỪNG nguồn này.",
                sourceKey, Short(fingerprint));
            return false;
        }

        _logger.LogInformation(
            "[Cấp 1] Nguồn {Source}: fingerprint đổi {Old} → {New} — tiếp tục Cấp 2.",
            sourceKey, Short(old), Short(fingerprint));

        if (snapshot == null)
        {
            _db.ComplianceSourceSnapshots.Add(new SourceSnapshot
            {
                SourceKey = sourceKey,
                Fingerprint = fingerprint,
                CheckedAt = DateTime.UtcNow,
            });
        }
        else
        {
            snapshot.Fingerprint = fingerprint;
            snapshot.CheckedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string Short(string hash) => hash.Length <= 12 ? hash : hash[..12];
}
