using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class RolePermissionRepository : IRolePermissionRepository
{
    private readonly AppDbContext _context;

    public RolePermissionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RolePermission>> GetByRoleIdAsync(int roleId, CancellationToken cancellationToken = default) =>
        await _context.RolePermissions
            .AsNoTracking()
            .Include(rp => rp.Permission)
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetPermissionCodesByRoleIdAsync(int roleId, CancellationToken cancellationToken = default) =>
        await _context.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission.Code)
            .ToListAsync(cancellationToken);

    public async Task ReplaceRolePermissionsAsync(int roleId, IEnumerable<int> permissionIds, CancellationToken cancellationToken = default)
    {
        var existing = await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(cancellationToken);

        _context.RolePermissions.RemoveRange(existing);

        var toAdd = permissionIds
            .Distinct()
            .Select(pid => new RolePermission { RoleId = roleId, PermissionId = pid });

        await _context.RolePermissions.AddRangeAsync(toAdd, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteByRoleIdAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var existing = await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(cancellationToken);

        _context.RolePermissions.RemoveRange(existing);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
