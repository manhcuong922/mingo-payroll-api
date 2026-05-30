using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.EmployeeInformation)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken = default) =>
        _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.EmployeeInformation)
            .FirstOrDefaultAsync(u => u.EmployeeCode == employeeCode, cancellationToken);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.EmployeeInformation)
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.EmployeeInformation)
            .ToListAsync(cancellationToken);

    public Task<bool> EmployeeCodeExistsAsync(string employeeCode, int? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Users.AsNoTracking().Where(u => u.EmployeeCode == employeeCode);
        if (excludeUserId is not null)
            query = query.Where(u => u.Id != excludeUserId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> UsernameExistsAsync(string username, int? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Users.AsNoTracking().Where(u => u.Username == username);
        if (excludeUserId is not null)
            query = query.Where(u => u.Id != excludeUserId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, int? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Users.AsNoTracking().Where(u => u.Email == email);
        if (excludeUserId is not null)
            query = query.Where(u => u.Id != excludeUserId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> RoleExistsAsync(int roleId, CancellationToken cancellationToken = default) =>
        _context.Roles.AsNoTracking().AnyAsync(r => r.Id == roleId, cancellationToken);

    public async Task<int> AddAsync(User entity, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(User entity, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Users.FirstOrDefaultAsync(u => u.Id == entity.Id, cancellationToken);
        if (existing is null) return false;

        existing.EmployeeCode = entity.EmployeeCode;
        existing.FullName = entity.FullName;
        existing.RoleId = entity.RoleId;
        existing.Username = entity.Username;
        existing.Email = entity.Email;
        existing.Dob = entity.Dob;
        existing.PhoneNo = entity.PhoneNo;
        existing.Status = entity.Status;

        if (!string.IsNullOrEmpty(entity.PasswordHash))
        {
            existing.PasswordHash = entity.PasswordHash;
            existing.PasswordChangedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateLastLoginAsync(int userId, DateTime loginAt, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (existing is null) return false;

        existing.LastLoginAt = loginAt;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (existing is null) return false;

        _context.Users.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
