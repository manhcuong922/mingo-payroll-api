using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _context;

    public RoleRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Role?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Role?> GetByIdWithPermissionsAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        _context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Name == name, cancellationToken);

    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Roles.AsNoTracking().OrderBy(r => r.Id).ToListAsync(cancellationToken);

    public Task<bool> NameExistsAsync(string name, int? excludeRoleId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Roles.AsNoTracking().Where(r => r.Name == name);
        if (excludeRoleId is not null)
            query = query.Where(r => r.Id != excludeRoleId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> HasUsersAsync(int roleId, CancellationToken cancellationToken = default) =>
        _context.Users.AsNoTracking().AnyAsync(u => u.RoleId == roleId, cancellationToken);

    public async Task<IReadOnlyList<int>> GetUserIdsByRoleAsync(int roleId, CancellationToken cancellationToken = default) =>
        await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == roleId)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

    public async Task<int> AddAsync(Role entity, CancellationToken cancellationToken = default)
    {
        await _context.Roles.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(Role entity, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Roles.FirstOrDefaultAsync(r => r.Id == entity.Id, cancellationToken);
        if (existing is null) return false;

        existing.Name = entity.Name;
        existing.Description = entity.Description;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (existing is null) return false;

        _context.Roles.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
