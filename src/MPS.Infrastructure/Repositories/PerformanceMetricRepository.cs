using System.Data;
using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class PerformanceMetricRepository : IPerformanceMetricRepository
{
    private readonly AppDbContext _context;

    public PerformanceMetricRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<PerformanceMetric?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.PerformanceMetrics
            .AsNoTracking()
            .Include(p => p.TargetRoles)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PerformanceMetric>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.PerformanceMetrics
            .AsNoTracking()
            .Include(p => p.TargetRoles)
            .ToListAsync(cancellationToken);

    public async Task<decimal> GetTotalWeightByTypeExcludingAsync(
        int typeId,
        int? excludeId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PerformanceMetrics
            .AsNoTracking()
            .Where(p => p.TypePerformanceMetricId == typeId);
        if (excludeId.HasValue)
            query = query.Where(p => p.Id != excludeId.Value);
        return await query.SumAsync(p => p.Weight, cancellationToken);
    }

    public async Task<bool> AllRolesExistAsync(
        IEnumerable<int> roleIds,
        CancellationToken cancellationToken = default)
    {
        var ids = roleIds.Distinct().ToArray();
        if (ids.Length == 0) return true;
        var count = await _context.Roles
            .AsNoTracking()
            .CountAsync(r => ids.Contains(r.Id), cancellationToken);
        return count == ids.Length;
    }

    public async Task<int> AddAsync(
        PerformanceMetric entity,
        IEnumerable<int> targetRoleIds,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var existingTotal = await _context.PerformanceMetrics
            .Where(p => p.TypePerformanceMetricId == entity.TypePerformanceMetricId)
            .SumAsync(p => p.Weight, cancellationToken);

        if (existingTotal + entity.Weight > 100)
            throw new DomainValidationException(
                $"Total weight within this type would exceed 100. Current total: {existingTotal}.");

        entity.TargetRoles = await LoadRolesAsync(targetRoleIds, cancellationToken);
        await _context.PerformanceMetrics.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(
        PerformanceMetric entity,
        IEnumerable<int> targetRoleIds,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var existing = await _context.PerformanceMetrics
            .Include(p => p.TargetRoles)
            .FirstOrDefaultAsync(p => p.Id == entity.Id, cancellationToken);

        if (existing is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return false;
        }

        var totalExcludingSelf = await _context.PerformanceMetrics
            .Where(p => p.TypePerformanceMetricId == entity.TypePerformanceMetricId
                        && p.Id != entity.Id)
            .SumAsync(p => p.Weight, cancellationToken);

        if (totalExcludingSelf + entity.Weight > 100)
            throw new DomainValidationException(
                $"Total weight within this type would exceed 100. Others total: {totalExcludingSelf}.");

        existing.Name = entity.Name;
        existing.Description = entity.Description;
        existing.Weight = entity.Weight;
        existing.TypePerformanceMetricId = entity.TypePerformanceMetricId;

        var newRoles = await LoadRolesAsync(targetRoleIds, cancellationToken);
        existing.TargetRoles.Clear();
        foreach (var role in newRoles)
            existing.TargetRoles.Add(role);

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.PerformanceMetrics
            .Include(p => p.TargetRoles)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (existing is null) return false;

        _context.PerformanceMetrics.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<List<Role>> LoadRolesAsync(
        IEnumerable<int> roleIds,
        CancellationToken cancellationToken)
    {
        var ids = roleIds?.Distinct().ToArray() ?? Array.Empty<int>();
        if (ids.Length == 0) return new List<Role>();
        return await _context.Roles
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(cancellationToken);
    }
}
