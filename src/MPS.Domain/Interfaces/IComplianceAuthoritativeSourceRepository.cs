using MPS.Domain.Entities.Compliance.AutoUpdate;

namespace MPS.Domain.Interfaces;

/// <summary>Đọc/ghi "văn bản đang chi phối" cho mỗi loại entity compliance (móng cho Kênh B).</summary>
public interface IComplianceAuthoritativeSourceRepository
{
    Task<IReadOnlyList<ComplianceAuthoritativeSource>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Upsert theo <paramref name="targetEntityType"/>: chưa có thì tạo, có rồi thì cập nhật
    /// văn bản chi phối + LastVerifiedAt. Lưu ngay.
    /// </summary>
    Task UpsertAsync(
        string targetEntityType, int? legalDocumentId, string? documentNo, string? documentUrl,
        DateTime verifiedAt, CancellationToken cancellationToken);
}
