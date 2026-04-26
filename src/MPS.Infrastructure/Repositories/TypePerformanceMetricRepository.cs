using System.Data;
using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class TypePerformanceMetricRepository : ITypePerformanceMetricRepository
{
    private readonly AppDbContext _context;

    public TypePerformanceMetricRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<TypePerformanceMetric?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.TypePerformanceMetrics
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TypePerformanceMetric>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.TypePerformanceMetrics
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<decimal> GetTotalWeightExcludingAsync(int? excludeId, CancellationToken cancellationToken = default)
    {
        var query = _context.TypePerformanceMetrics.AsNoTracking().AsQueryable();
        if (excludeId.HasValue)
            query = query.Where(t => t.Id != excludeId.Value);
        return await query.SumAsync(t => t.Weight, cancellationToken);
    }

    public Task<bool> HasPerformanceMetricsAsync(int id, CancellationToken cancellationToken = default) =>
        _context.PerformanceMetrics
            .AsNoTracking()
            .AnyAsync(p => p.TypePerformanceMetricId == id, cancellationToken);

    public async Task<int> AddAsync(TypePerformanceMetric entity, CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var existingTotal = await _context.TypePerformanceMetrics
            .SumAsync(t => t.Weight, cancellationToken);

        if (existingTotal + entity.Weight > 100)
            throw new DomainValidationException(
                $"Total weight would exceed 100. Current total: {existingTotal}.");

        await _context.TypePerformanceMetrics.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(TypePerformanceMetric entity, CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var existing = await _context.TypePerformanceMetrics
            .FirstOrDefaultAsync(t => t.Id == entity.Id, cancellationToken);

        if (existing is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return false;
        }

        var totalExcludingSelf = await _context.TypePerformanceMetrics
            .Where(t => t.Id != entity.Id)
            .SumAsync(t => t.Weight, cancellationToken);

        if (totalExcludingSelf + entity.Weight > 100)
            throw new DomainValidationException(
                $"Total weight would exceed 100. Other types total: {totalExcludingSelf}.");

        existing.Name = entity.Name;
        existing.Weight = entity.Weight;

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.TypePerformanceMetrics
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (existing is null) return false;

        _context.TypePerformanceMetrics.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
