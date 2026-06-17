using MPS.Domain.Entities.Compliance;

namespace MPS.Domain.Interfaces;

public interface ILegalDocumentRepository
{
    Task<LegalDocument?> GetByIdAsync(int id, CancellationToken ct);
    Task<LegalDocument?> GetByCodeAsync(string code, CancellationToken ct);

    Task<(IReadOnlyList<LegalDocument> Items, int TotalCount)> GetAllAsync(
        int pageNumber, int pageSize, string? documentType, bool? isActive, string? keyword, CancellationToken ct);

    Task<IReadOnlyList<LegalDocument>> GetActiveAsync(CancellationToken ct);

    Task<IReadOnlyList<LegalDocument>> GetByComplianceVersionAsync(string entityType, int versionId, CancellationToken ct);

    // Returns (ComplianceEntityType, ComplianceVersionId) pairs — mapped to DTO at Application layer
    Task<IReadOnlyList<(string EntityType, int VersionId)>> GetVersionsByDocumentAsync(int legalDocumentId, CancellationToken ct);

    Task<int> AddAsync(LegalDocument entity, CancellationToken ct);
    Task<bool> DeactivateAsync(int id, CancellationToken ct);
    Task<int> LinkToVersionAsync(ComplianceVersionLegalDocument link, CancellationToken ct);
    Task<bool> UnlinkFromVersionAsync(string entityType, int versionId, int legalDocumentId, CancellationToken ct);
}
