using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Compliance;

public sealed class ComplianceAuthoritativeSourceRepository : IComplianceAuthoritativeSourceRepository
{
    private readonly AppDbContext _db;

    public ComplianceAuthoritativeSourceRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ComplianceAuthoritativeSource>> GetAllAsync(CancellationToken cancellationToken) =>
        await _db.ComplianceAuthoritativeSources
            .AsNoTracking()
            .OrderBy(s => s.TargetEntityType)
            .ToListAsync(cancellationToken);

    public async Task UpsertAsync(
        string targetEntityType, int? legalDocumentId, string? documentNo, string? documentUrl,
        DateTime verifiedAt, CancellationToken cancellationToken)
    {
        var existing = await _db.ComplianceAuthoritativeSources
            .FirstOrDefaultAsync(s => s.TargetEntityType == targetEntityType, cancellationToken);

        if (existing == null)
        {
            _db.ComplianceAuthoritativeSources.Add(new ComplianceAuthoritativeSource
            {
                TargetEntityType = targetEntityType,
                LegalDocumentId = legalDocumentId,
                DocumentNo = documentNo,
                DocumentUrl = documentUrl,
                LastVerifiedAt = verifiedAt,
                UpdatedAt = verifiedAt,
            });
        }
        else
        {
            existing.LegalDocumentId = legalDocumentId;
            existing.DocumentNo = documentNo;
            existing.DocumentUrl = documentUrl;
            existing.LastVerifiedAt = verifiedAt;
            existing.UpdatedAt = verifiedAt;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
