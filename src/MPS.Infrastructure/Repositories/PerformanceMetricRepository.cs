using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MPS.Infrastructure.Repositories;

/// <summary>
/// Repository Implementation cho PerformanceMetric sử dụng EF Core + SQL Server.
/// Load Role từ DB thay vì tạo stub (new Role { Id = id }) để tránh INSERT trùng khi EF tracking.
/// </summary>
public class PerformanceMetricRepository : IPerformanceMetricRepository
{
    private readonly ExampleDbContext _context;

    public PerformanceMetricRepository(ExampleDbContext context)
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

    public async Task<int> AddAsync(
        PerformanceMetric entity,
        IEnumerable<int> targetRoleIds,
        CancellationToken cancellationToken = default)
    {
        entity.TargetRoles = await LoadRolesAsync(targetRoleIds, cancellationToken);

        await _context.PerformanceMetrics.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(
        PerformanceMetric entity,
        IEnumerable<int> targetRoleIds,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.PerformanceMetrics
            .Include(p => p.TargetRoles)
            .FirstOrDefaultAsync(p => p.Id == entity.Id, cancellationToken);

        if (existing is null) return false;

        existing.Name = entity.Name;
        existing.Description = entity.Description;
        existing.Weight = entity.Weight;

        var newRoles = await LoadRolesAsync(targetRoleIds, cancellationToken);
        existing.TargetRoles.Clear();
        foreach (var role in newRoles)
        {
            existing.TargetRoles.Add(role);
        }

        await _context.SaveChangesAsync(cancellationToken);
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

        var roles = await _context.Roles
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(cancellationToken);

        if (roles.Count != ids.Length)
        {
            var missing = ids.Except(roles.Select(r => r.Id));
            throw new InvalidOperationException(
                $"Role(s) not found: {string.Join(", ", missing)}");
        }

        return roles;
    }
}
