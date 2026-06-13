using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Compliance;

public sealed class AllowanceExemptThresholdRepository : IAllowanceExemptThresholdRepository
{
    private readonly AppDbContext _context;
    private readonly IClock _clock;

    public AllowanceExemptThresholdRepository(AppDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public Task<AllowanceExemptThresholdVersion?> GetByIdAsync(int id, CancellationToken ct) =>
        _context.AllowanceExemptThresholdVersions
            .AsNoTracking()
            .Include(x => x.AllowanceType)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<AllowanceExemptThresholdVersion>> GetAllAsync(bool includeArchived, CancellationToken ct)
    {
        var query = _context.AllowanceExemptThresholdVersions
            .AsNoTracking()
            .Include(x => x.AllowanceType)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .AsQueryable();
        if (!includeArchived)
            query = query.Where(x => !x.IsArchived);
        return await query.OrderByDescending(x => x.ValidFrom).ToListAsync(ct);
    }

    public Task<AllowanceExemptThresholdVersion?> GetActiveForAllowanceTypeAsync(int allowanceTypeId, CancellationToken ct) =>
        _context.AllowanceExemptThresholdVersions
            .AsNoTracking()
            .Include(x => x.AllowanceType)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .FirstOrDefaultAsync(x => x.AllowanceTypeId == allowanceTypeId && x.IsActive && !x.IsArchived, ct);

    public async Task<AllowanceExemptThresholdVersion?> GetActiveAtForAllowanceTypeAsync(int allowanceTypeId, DateOnly date, CancellationToken ct)
    {
        var active = await _context.AllowanceExemptThresholdVersions
            .AsNoTracking()
            .Include(x => x.AllowanceType)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .Where(x => x.AllowanceTypeId == allowanceTypeId && x.IsActive && !x.IsArchived && x.ValidFrom <= date)
            .FirstOrDefaultAsync(ct);

        if (active is not null) return active;

        return await _context.AllowanceExemptThresholdVersions
            .AsNoTracking()
            .Include(x => x.AllowanceType)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .Where(x => x.AllowanceTypeId == allowanceTypeId && !x.IsActive && !x.IsArchived && x.ValidFrom <= date)
            .OrderByDescending(x => x.ValidFrom)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> AddAsync(AllowanceExemptThresholdVersion entity, bool applyImmediately, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        entity.IsArchived = false;
        entity.CreatedAt = _clock.UtcNow;

        // AllowanceExemptThreshold chỉ hỗ trợ applyImmediately=true (validated ở Application layer)
        entity.ValidFrom = today;
        entity.IsActive = true;

        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var current = await _context.AllowanceExemptThresholdVersions
                .Where(x => x.AllowanceTypeId == entity.AllowanceTypeId && x.IsActive && !x.IsArchived)
                .FirstOrDefaultAsync(ct);

            if (current is not null)
            {
                current.IsActive = false;
                current.UpdatedAt = _clock.UtcNow;
            }

            await _context.AllowanceExemptThresholdVersions.AddAsync(entity, ct);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException dbEx) when (IsUniqueIndexViolation(dbEx))
        {
            await tx.RollbackAsync(ct);
            throw new ConflictException("A threshold version with this AllowanceType and effective date already exists. Please refresh and retry.");
        }

        return entity.Id;
    }

    public async Task<bool> UpdateAsync(AllowanceExemptThresholdVersion entity, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var tracked = await _context.AllowanceExemptThresholdVersions
            .FirstOrDefaultAsync(x => x.Id == entity.Id, ct)
            ?? throw new NotFoundException($"AllowanceExemptThresholdVersion {entity.Id} not found.");

        if (tracked.IsActive)
            throw new ConflictException("Active version is read-only. Create a new version instead.");
        if (tracked.IsArchived)
            throw new ConflictException("Archived version cannot be modified.");
        if (tracked.ValidFrom <= today)
            throw new ConflictException("Historical version is read-only — immutable for audit integrity.");

        tracked.ThresholdMode = entity.ThresholdMode;
        tracked.MonthlyExemptAmount = entity.MonthlyExemptAmount;
        tracked.AppliesToTax = entity.AppliesToTax;
        tracked.AppliesToSocial = entity.AppliesToSocial;
        tracked.LegalReference = entity.LegalReference;
        tracked.Note = entity.Note;
        tracked.UpdatedAt = _clock.UtcNow;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeletePendingAsync(int id, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var target = await _context.AllowanceExemptThresholdVersions
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"AllowanceExemptThresholdVersion {id} not found.");

        if (target.IsActive)
            throw new ConflictException("Cannot delete active version. It is currently in effect.");
        if (target.ValidFrom <= today)
            throw new ConflictException("Cannot delete historical version — immutable for audit integrity.");

        _context.AllowanceExemptThresholdVersions.Remove(target);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private static bool IsUniqueIndexViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException as SqlException;
        return inner is not null && (inner.Number == 2601 || inner.Number == 2627);
    }
}
