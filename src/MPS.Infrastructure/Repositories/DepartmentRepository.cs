using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly AppDbContext _context;

    public DepartmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Department>> GetAllAsync(bool includeInactive, CancellationToken ct)
    {
        var query = _context.Departments
            .AsNoTracking()
            .Include(d => d.Manager)
            .Include(d => d.Employees)
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(d => d.IsActive);

        return await query.OrderBy(d => d.Name).ToListAsync(ct);
    }

    public Task<Department?> GetByIdAsync(int id, CancellationToken ct) =>
        _context.Departments
            .AsNoTracking()
            .Include(d => d.Manager)
            .Include(d => d.Employees)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<Department?> GetByManagerEmployeeIdAsync(int employeeId, CancellationToken ct) =>
        _context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ManagerId == employeeId && d.IsActive, ct);

    public async Task<(IReadOnlyList<Department> Items, int Total)> GetPagedAsync(
        string? keyword, bool includeInactive, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.Departments
            .AsNoTracking()
            .Include(d => d.Manager)
            .Include(d => d.Employees)
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(d => d.IsActive);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(d =>
                d.Code.ToLower().Contains(kw) ||
                d.Name.ToLower().Contains(kw));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(d => d.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<bool> CodeExistsAsync(string code, int? excludeId, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var query = _context.Departments
            .AsNoTracking()
            .Where(d => d.Code == normalized);

        if (excludeId.HasValue)
            query = query.Where(d => d.Id != excludeId.Value);

        return query.AnyAsync(ct);
    }

    public Task<bool> HasEmployeesAsync(int id, CancellationToken ct) =>
        _context.EmployeeInformations
            .AsNoTracking()
            .AnyAsync(e => e.DepartmentId == id, ct);

    public Task<bool> ManagerExistsWithUserAsync(int managerEmployeeId, CancellationToken ct) =>
        _context.EmployeeInformations
            .AsNoTracking()
            .AnyAsync(e => e.Id == managerEmployeeId && e.UserId != null, ct);

    public async Task<int> AddAsync(Department entity, CancellationToken ct)
    {
        await _context.Departments.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(Department entity, CancellationToken ct)
    {
        var existing = await _context.Departments
            .FirstOrDefaultAsync(d => d.Id == entity.Id, ct);

        if (existing is null) return false;

        existing.Code = entity.Code;
        existing.Name = entity.Name;
        existing.Description = entity.Description;
        existing.ManagerId = entity.ManagerId;

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ArchiveAsync(int id, CancellationToken ct)
    {
        var existing = await _context.Departments
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (existing is null) return false;

        // Soft-delete: giữ bản ghi để đơn/task cũ vẫn tham chiếu được xưởng.
        existing.IsActive = false;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    // ===== Bắc cầu User ↔ Department =====

    public Task<Department?> GetByManagerUserIdAsync(int userId, CancellationToken ct) =>
        _context.Departments
            .AsNoTracking()
            .Where(d => d.IsActive && d.Manager != null && d.Manager.UserId == userId)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<int>> GetMemberUserIdsAsync(int departmentId, CancellationToken ct) =>
        await _context.EmployeeInformations
            .AsNoTracking()
            .Where(e => e.DepartmentId == departmentId && e.UserId != null)
            .Select(e => e.UserId!.Value)
            .ToListAsync(ct);
}
