using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Compliance;

public sealed class MinimumWageRepository : IMinimumWageRepository
{
    private readonly AppDbContext _context;
    private readonly IClock _clock;

    public MinimumWageRepository(AppDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public Task<MinimumWageVersion?> GetByIdAsync(int id, CancellationToken ct) =>
        _context.MinimumWageVersions
            .AsNoTracking()
            .Include(x => x.Regions)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<MinimumWageVersion>> GetAllAsync(bool includeArchived, CancellationToken ct)
    {
        var query = _context.MinimumWageVersions
            .AsNoTracking()
            .Include(x => x.Regions)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .AsQueryable();
        if (!includeArchived)
            query = query.Where(x => !x.IsArchived);
        return await query.OrderByDescending(x => x.ValidFrom).ToListAsync(ct);
    }

    public Task<MinimumWageVersion?> GetActiveAsync(CancellationToken ct) =>
        _context.MinimumWageVersions
            .AsNoTracking()
            .Include(x => x.Regions)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .FirstOrDefaultAsync(x => x.IsActive && !x.IsArchived, ct);

    public async Task<MinimumWageVersion?> GetActiveAtAsync(DateOnly date, CancellationToken ct)
    {
        var active = await _context.MinimumWageVersions
            .AsNoTracking()
            .Include(x => x.Regions)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .Where(x => x.IsActive && !x.IsArchived && x.ValidFrom <= date)
            .FirstOrDefaultAsync(ct);

        if (active is not null) return active;

        return await _context.MinimumWageVersions
            .AsNoTracking()
            .Include(x => x.Regions)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .Where(x => !x.IsActive && !x.IsArchived && x.ValidFrom <= date)
            .OrderByDescending(x => x.ValidFrom)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> AddAsync(MinimumWageVersion entity, bool applyImmediately, CancellationToken ct)
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
                var current = await _context.MinimumWageVersions
                    .Where(x => x.IsActive && !x.IsArchived)
                    .FirstOrDefaultAsync(ct);

                if (current is not null)
                {
                    current.IsActive = false;
                    current.UpdatedAt = _clock.UtcNow;
                }

                await _context.MinimumWageVersions.AddAsync(entity, ct);
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

            var duplicate = await _context.MinimumWageVersions
                .Where(x => x.ValidFrom == entity.ValidFrom && !x.IsArchived)
                .AnyAsync(ct);

            if (duplicate)
                throw new ConflictException($"A pending version with ValidFrom={entity.ValidFrom:yyyy-MM-dd} already exists.");

            await _context.MinimumWageVersions.AddAsync(entity, ct);
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

    public async Task<bool> UpdateAsync(MinimumWageVersion entity, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var tracked = await _context.MinimumWageVersions
            .Include(x => x.Regions)
            .FirstOrDefaultAsync(x => x.Id == entity.Id, ct)
            ?? throw new NotFoundException($"MinimumWageVersion {entity.Id} not found.");

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

        _context.MinimumWageRegions.RemoveRange(tracked.Regions);
        foreach (var r in entity.Regions)
        {
            r.MinimumWageVersionId = tracked.Id;
            await _context.MinimumWageRegions.AddAsync(r, ct);
        }

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeletePendingAsync(int id, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var target = await _context.MinimumWageVersions
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"MinimumWageVersion {id} not found.");

        if (target.IsActive)
            throw new ConflictException("Cannot delete active version. It is currently in effect.");
        if (target.ValidFrom <= today)
            throw new ConflictException("Cannot delete historical version — immutable for audit integrity.");

        _context.MinimumWageVersions.Remove(target);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private static bool IsUniqueIndexViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException as SqlException;
        return inner is not null && (inner.Number == 2601 || inner.Number == 2627);
    }
}
