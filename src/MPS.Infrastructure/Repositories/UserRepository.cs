using System.Data;
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

    public async Task<IReadOnlyDictionary<string, int>> GetEmployeeInformationIdsByCodesAsync(
        IEnumerable<string> employeeCodes,
        CancellationToken cancellationToken = default)
    {
        var distinct = employeeCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (distinct.Length == 0)
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var rows = await _context.Users
            .AsNoTracking()
            .Where(u => distinct.Contains(u.EmployeeCode) && u.EmployeeInformation != null)
            .Select(u => new { u.EmployeeCode, EmployeeInformationId = u.EmployeeInformation!.Id })
            .ToListAsync(cancellationToken);

        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
            map[row.EmployeeCode] = row.EmployeeInformationId;
        return map;
    }

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

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(
        string? keyword,
        int? roleId,
        int? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.EmployeeInformation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(u =>
                EF.Functions.Like(u.FullName, $"%{kw}%") ||
                EF.Functions.Like(u.Username, $"%{kw}%") ||
                EF.Functions.Like(u.EmployeeCode, $"%{kw}%") ||
                EF.Functions.Like(u.Email, $"%{kw}%"));
        }

        if (roleId.HasValue)
            query = query.Where(u => u.RoleId == roleId.Value);

        if (status.HasValue)
            query = query.Where(u => u.Status == status.Value);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

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

    public Task<int> CountByEmployeeCodePrefixAsync(string prefix, CancellationToken cancellationToken = default) =>
        _context.Users
            .AsNoTracking()
            .Where(u => u.EmployeeCode.StartsWith(prefix))
            .CountAsync(cancellationToken);

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

    public async Task<bool> UpdatePasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (existing is null) return false;

        existing.PasswordHash = passwordHash;
        existing.PasswordChangedAt = DateTime.UtcNow;
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

    public async Task<int> AddWithEmployeeInfoLinkAsync(User user, int employeeInformationId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await _context.Users.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await _context.EmployeeInformations
            .Where(e => e.Id == employeeInformationId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.UserId, user.Id), cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return user.Id;
    }
}
