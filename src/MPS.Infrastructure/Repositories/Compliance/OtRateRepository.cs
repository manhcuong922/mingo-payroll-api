using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Compliance;

public sealed class OtRateRepository : IOtRateRepository
{
    private readonly AppDbContext _context;
    private readonly IClock _clock;

    public OtRateRepository(AppDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public Task<OtRateVersion?> GetByIdAsync(int id, CancellationToken ct) =>
        _context.OtRateVersions
            .AsNoTracking()
            .Include(x => x.Entries)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<OtRateVersion>> GetAllAsync(bool includeArchived, CancellationToken ct)
    {
        var query = _context.OtRateVersions
            .AsNoTracking()
            .Include(x => x.Entries)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .AsQueryable();
        if (!includeArchived)
            query = query.Where(x => !x.IsArchived);
        return await query.OrderByDescending(x => x.ValidFrom).ToListAsync(ct);
    }

    public Task<OtRateVersion?> GetActiveAsync(CancellationToken ct) =>
        _context.OtRateVersions
            .AsNoTracking()
            .Include(x => x.Entries)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .FirstOrDefaultAsync(x => x.IsActive && !x.IsArchived, ct);

    public async Task<OtRateVersion?> GetActiveAtAsync(DateOnly date, CancellationToken ct)
    {
        var active = await _context.OtRateVersions
            .AsNoTracking()
            .Include(x => x.Entries)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .Where(x => x.IsActive && !x.IsArchived && x.ValidFrom <= date)
            .FirstOrDefaultAsync(ct);

        if (active is not null) return active;

        return await _context.OtRateVersions
            .AsNoTracking()
            .Include(x => x.Entries)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .Where(x => !x.IsActive && !x.IsArchived && x.ValidFrom <= date)
            .OrderByDescending(x => x.ValidFrom)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> AddAsync(OtRateVersion entity, bool applyImmediately, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        entity.IsArchived = false;
        entity.CreatedAt = _clock.UtcNow;

        if (applyImmediately)
        {
            entity.ValidFrom = today;
            entity.IsActive = true;

            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var current = await _context.OtRateVersions
                    .Where(x => x.IsActive && !x.IsArchived)
                    .FirstOrDefaultAsync(ct);

                if (current is not null)
                {
                    current.IsActive = false;
                    current.UpdatedAt = _clock.UtcNow;
                }

                await _context.OtRateVersions.AddAsync(entity, ct);
                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateException dbEx) when (IsUniqueIndexViolation(dbEx))
            {
                await tx.RollbackAsync(ct);
                throw new ConflictException("A compliance version with this effective date scope already exists. Please refresh and retry.");
            }
        }
        else
        {
            entity.IsActive = false;

            var duplicate = await _context.OtRateVersions
                .Where(x => x.ValidFrom == entity.ValidFrom && !x.IsArchived)
                .AnyAsync(ct);

            if (duplicate)
                throw new ConflictException($"A pending version with ValidFrom={entity.ValidFrom:yyyy-MM-dd} already exists.");

            await _context.OtRateVersions.AddAsync(entity, ct);
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException dbEx) when (IsUniqueIndexViolation(dbEx))
            {
                throw new ConflictException("A pending version with this ValidFrom already exists (concurrent insert). Please retry.");
            }
        }

        return entity.Id;
    }

    public async Task<bool> UpdateAsync(OtRateVersion entity, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var tracked = await _context.OtRateVersions
            .Include(x => x.Entries)
            .FirstOrDefaultAsync(x => x.Id == entity.Id, ct)
            ?? throw new NotFoundException($"OtRateVersion {entity.Id} not found.");

        if (tracked.IsActive)
            throw new ConflictException("Active version is read-only. Create a new version instead.");
        if (tracked.IsArchived)
            throw new ConflictException("Archived version cannot be modified.");
        if (tracked.ValidFrom <= today)
            throw new ConflictException("Historical version is read-only — immutable for audit integrity.");
        if (entity.ValidFrom <= today)
            throw new ConflictException($"ValidFrom must be strictly after today ({today:yyyy-MM-dd}).");

        tracked.ValidFrom = entity.ValidFrom;
        tracked.LegalReference = entity.LegalReference;
        tracked.Note = entity.Note;
        tracked.UpdatedAt = _clock.UtcNow;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        _context.OtRateEntries.RemoveRange(tracked.Entries);
        foreach (var e in entity.Entries)
        {
            e.OtRateVersionId = tracked.Id;
            await _context.OtRateEntries.AddAsync(e, ct);
        }

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeletePendingAsync(int id, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var target = await _context.OtRateVersions
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"OtRateVersion {id} not found.");

        if (target.IsActive)
            throw new ConflictException("Cannot delete active version. It is currently in effect.");
        if (target.ValidFrom <= today)
            throw new ConflictException("Cannot delete historical version — immutable for audit integrity.");

        _context.OtRateVersions.Remove(target);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private static bool IsUniqueIndexViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException as SqlException;
        return inner is not null && (inner.Number == 2601 || inner.Number == 2627);
    }
}
