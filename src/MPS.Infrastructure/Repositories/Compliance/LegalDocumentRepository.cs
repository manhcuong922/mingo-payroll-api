using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Compliance;

public sealed class LegalDocumentRepository : ILegalDocumentRepository
{
    private readonly AppDbContext _context;
    private readonly IClock _clock;

    public LegalDocumentRepository(AppDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public Task<LegalDocument?> GetByIdAsync(int id, CancellationToken ct) =>
        _context.LegalDocuments
            .AsNoTracking()
            .Include(d => d.CreatedByUser)
            .Include(d => d.UpdatedByUser)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<LegalDocument?> GetByCodeAsync(string code, CancellationToken ct) =>
        _context.LegalDocuments
            .AsNoTracking()
            .Include(d => d.CreatedByUser)
            .Include(d => d.UpdatedByUser)
            .FirstOrDefaultAsync(d => d.Code == code, ct);

    public async Task<IReadOnlyList<LegalDocument>> GetAllAsync(
        string? documentType, bool? isActive, CancellationToken ct)
    {
        var query = _context.LegalDocuments
            .AsNoTracking()
            .Include(d => d.CreatedByUser)
            .Include(d => d.UpdatedByUser)
            .AsQueryable();

        if (documentType is not null)
            query = query.Where(d => d.DocumentType == documentType);
        if (isActive is not null)
            query = query.Where(d => d.IsActive == isActive.Value);

        return await query.OrderByDescending(d => d.EffectiveDate).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LegalDocument>> GetByComplianceVersionAsync(
        string entityType, int versionId, CancellationToken ct) =>
        await _context.ComplianceVersionLegalDocuments
            .AsNoTracking()
            .Where(j => j.ComplianceEntityType == entityType && j.ComplianceVersionId == versionId)
            .Include(j => j.LegalDocument).ThenInclude(d => d.CreatedByUser)
            .Include(j => j.LegalDocument).ThenInclude(d => d.UpdatedByUser)
            .Select(j => j.LegalDocument)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<(string EntityType, int VersionId)>> GetVersionsByDocumentAsync(
        int legalDocumentId, CancellationToken ct) =>
        await _context.ComplianceVersionLegalDocuments
            .AsNoTracking()
            .Where(j => j.LegalDocumentId == legalDocumentId)
            .Select(j => ValueTuple.Create(j.ComplianceEntityType, j.ComplianceVersionId))
            .ToListAsync(ct);

    public async Task<int> AddAsync(LegalDocument entity, CancellationToken ct)
    {
        entity.CreatedAt = _clock.UtcNow;
        await _context.LegalDocuments.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(LegalDocument entity, CancellationToken ct)
    {
        var tracked = await _context.LegalDocuments.FindAsync([entity.Id], ct)
            ?? throw new NotFoundException($"LegalDocument {entity.Id} not found.");

        tracked.DocumentType = entity.DocumentType;
        tracked.DocumentNumber = entity.DocumentNumber;
        tracked.Title = entity.Title;
        tracked.IssuedBy = entity.IssuedBy;
        tracked.IssuedDate = entity.IssuedDate;
        tracked.EffectiveDate = entity.EffectiveDate;
        tracked.ExpiryDate = entity.ExpiryDate;
        tracked.Url = entity.Url;
        tracked.Note = entity.Note;
        tracked.IsActive = entity.IsActive;
        tracked.UpdatedAt = _clock.UtcNow;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct)
    {
        var doc = await _context.LegalDocuments.FindAsync([id], ct)
            ?? throw new NotFoundException($"LegalDocument {id} not found.");

        doc.IsActive = false;
        doc.UpdatedAt = _clock.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> LinkToVersionAsync(ComplianceVersionLegalDocument link, CancellationToken ct)
    {
        var exists = await _context.ComplianceVersionLegalDocuments
            .AnyAsync(j => j.ComplianceEntityType == link.ComplianceEntityType
                        && j.ComplianceVersionId == link.ComplianceVersionId
                        && j.LegalDocumentId == link.LegalDocumentId, ct);

        if (exists)
            throw new ConflictException(
                $"LegalDocument {link.LegalDocumentId} is already linked to {link.ComplianceEntityType} version {link.ComplianceVersionId}.");

        link.LinkedAt = _clock.UtcNow;
        await _context.ComplianceVersionLegalDocuments.AddAsync(link, ct);
        await _context.SaveChangesAsync(ct);
        return link.Id;
    }

    public async Task<bool> UnlinkFromVersionAsync(
        string entityType, int versionId, int legalDocumentId, CancellationToken ct)
    {
        var link = await _context.ComplianceVersionLegalDocuments
            .FirstOrDefaultAsync(j => j.ComplianceEntityType == entityType
                                   && j.ComplianceVersionId == versionId
                                   && j.LegalDocumentId == legalDocumentId, ct)
            ?? throw new NotFoundException(
                $"Link between {entityType} version {versionId} and LegalDocument {legalDocumentId} not found.");

        _context.ComplianceVersionLegalDocuments.Remove(link);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
