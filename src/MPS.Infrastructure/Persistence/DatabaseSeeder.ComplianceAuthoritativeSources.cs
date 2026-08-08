using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Entities.Compliance.AutoUpdate;

namespace MPS.Infrastructure.Persistence;

public sealed partial class DatabaseSeeder
{
    /// <summary>
    /// Văn bản CHI PHỐI chính của từng loại entity — map tường minh sang <c>LegalDocument.Code</c>
    /// đã seed ở <see cref="SeedLegalDocumentsAsync"/>.
    ///
    /// Cố tình KHÔNG suy ra bằng "văn bản mới nhất đang link": bảng link chứa cả dữ liệu test
    /// (version [E2E] của ngưỡng phụ cấp link nhầm sang NĐ lương tối thiểu) nên suy diễn sẽ ra sai nguồn.
    /// Map cứng theo Code là thứ ổn định và đọc ra hiểu ngay đang rà soát văn bản nào.
    /// </summary>
    private static readonly (string TargetType, string LegalDocumentCode)[] AuthoritativeSourceMap =
    {
        // Tỷ lệ đóng nằm ở nghị định hướng dẫn — đây mới là văn bản hay bị thay thế.
        (TargetEntityTypes.SocialInsuranceRateVersion, "ND-158-2025"),
        (TargetEntityTypes.MinimumWageVersion, "ND-293-2025"),
        // Biểu thuế lũy tiến + giảm trừ do nghị quyết UBTVQH quy định.
        (TargetEntityTypes.PitRegulationVersion, "NQ-110-2025"),
        (TargetEntityTypes.LaborLawConstantsVersion, "BLLD-2019"),
        (TargetEntityTypes.AllowanceExemptThresholdVersion, "TT-26-2016"),
    };

    /// <summary>
    /// Seed "văn bản đang chi phối" cho từng loại entity compliance — MÓNG BẮT BUỘC của Kênh B:
    /// <c>ComplianceWatcher.RunOnceAsync</c> iterate đúng bảng này, bảng rỗng thì "Dò ngay" chạy 0 vòng,
    /// không gọi LLM và không ra kết quả nào.
    ///
    /// Bình thường bảng chỉ được upsert khi duyệt proposal → gà-và-trứng (chưa có nguồn thì không dò
    /// được, không dò được thì không bao giờ có proposal để sinh nguồn). Seed này phá vòng lặp đó.
    ///
    /// Idempotent theo từng loại: loại nào đã có bản ghi thì bỏ qua, không ghi đè kết quả mà
    /// watcher/applier đã cập nhật.
    /// </summary>
    private async Task SeedComplianceAuthoritativeSourcesAsync(CancellationToken ct)
    {
        var existingTypes = await _context.ComplianceAuthoritativeSources
            .AsNoTracking()
            .Select(s => s.TargetEntityType)
            .ToListAsync(ct);

        var missing = AuthoritativeSourceMap
            .Where(m => !existingTypes.Contains(m.TargetType))
            .ToList();

        if (missing.Count == 0)
        {
            _logger.LogDebug("ComplianceAuthoritativeSource đã đủ các loại entity; bỏ qua seed.");
            return;
        }

        var codes = missing.Select(m => m.LegalDocumentCode).ToList();
        var docs = await _context.LegalDocuments
            .AsNoTracking()
            .Where(d => codes.Contains(d.Code))
            .Select(d => new { d.Code, d.Id, d.DocumentNumber, d.Title, d.Url })
            .ToDictionaryAsync(d => d.Code, ct);

        var now = DateTime.UtcNow;
        var seeded = 0;

        foreach (var (targetType, code) in missing)
        {
            if (!docs.TryGetValue(code, out var doc))
            {
                _logger.LogWarning(
                    "Không tìm thấy LegalDocument Code={Code} — bỏ qua seed nguồn cho {TargetType}.",
                    code, targetType);
                continue;
            }

            await _context.ComplianceAuthoritativeSources.AddAsync(new ComplianceAuthoritativeSource
            {
                TargetEntityType = targetType,
                LegalDocumentId = doc.Id,
                DocumentNo = ResolveDocumentNo(doc.DocumentNumber, doc.Title),
                DocumentUrl = string.IsNullOrWhiteSpace(doc.Url) ? null : doc.Url,
                LastVerifiedAt = null,
                UpdatedAt = now,
                Note = $"Seed móng Kênh B từ LegalDocument {code}.",
            }, ct);
            seeded++;
        }

        if (seeded == 0) return;

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} ComplianceAuthoritativeSource (móng Kênh B).", seeded);
    }

    /// <summary>
    /// Số hiệu đưa vào prompt của watcher. Văn bản không có số hiệu (Bộ luật Lao động ghi "(không số)")
    /// thì dùng tiêu đề — LLM cần một định danh có nghĩa để biết đang đối chiếu với cái gì.
    /// </summary>
    private static string ResolveDocumentNo(string? documentNumber, string title)
    {
        var no = documentNumber?.Trim();
        var meaningless = string.IsNullOrEmpty(no)
            || no.Contains("không số", StringComparison.OrdinalIgnoreCase);
        return meaningless ? title : no!;
    }
}
