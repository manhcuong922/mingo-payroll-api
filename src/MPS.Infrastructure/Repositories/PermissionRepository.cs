using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class PermissionRepository : IPermissionRepository
{
    private readonly AppDbContext _context;

    public PermissionRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Permission?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Permissions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Permission?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        _context.Permissions.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code, cancellationToken);

    public async Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Resource).ThenBy(p => p.Action).ThenBy(p => p.Scope)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Permission>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        return await _context.Permissions
            .AsNoTracking()
            .Where(p => idList.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }
}
