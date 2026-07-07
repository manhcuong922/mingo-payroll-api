using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _context;

    public ProjectRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct, bool includeCancelled = false)
    {
        var query = _context.Projects
            .AsNoTracking()
            .Include(p => p.Department)
            .Include(p => p.CreatedBy)
            .AsQueryable();

        if (!includeCancelled)
            query = query.Where(p => p.DeletedAt == null);

        return await query.OrderByDescending(p => p.Id).ToListAsync(ct);
    }

    public Task<Project?> GetByIdAsync(int id, CancellationToken ct) =>
        _context.Projects
            .AsNoTracking()
            .Include(p => p.Department)
            .Include(p => p.CreatedBy)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Project?> GetByIdWithTasksAsync(int id, CancellationToken ct) =>
        _context.Projects
            .AsNoTracking()
            .Include(p => p.Department)
            .Include(p => p.CreatedBy)
            .Include(p => p.CompletedBy)
            .Include(p => p.Tasks).ThenInclude(t => t.Assignees).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Project>> GetByDepartmentAsync(
        int departmentId, CancellationToken ct, bool includeCancelled = false)
    {
        var query = _context.Projects
            .AsNoTracking()
            .Include(p => p.Department)
            .Include(p => p.CreatedBy)
            .Where(p => p.DepartmentId == departmentId)
            .AsQueryable();

        if (!includeCancelled)
            query = query.Where(p => p.DeletedAt == null);

        return await query.OrderByDescending(p => p.Id).ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<Project> Items, int Total)> GetPagedAsync(
        string? keyword, int? departmentId, int page, int pageSize, CancellationToken ct, bool includeCancelled = false)
    {
        var query = _context.Projects
            .AsNoTracking()
            .Include(p => p.Department)
            .Include(p => p.CreatedBy)
            .AsQueryable();

        if (departmentId.HasValue)
            query = query.Where(p => p.DepartmentId == departmentId);

        if (!includeCancelled)
            query = query.Where(p => p.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(p => p.Title.ToLower().Contains(kw));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<bool> HasActiveByDepartmentAsync(int departmentId, CancellationToken ct) =>
        _context.Projects.AnyAsync(
            p => p.DepartmentId == departmentId && p.Status == ProjectStatus.Active, ct);

    public async Task<int> AddAsync(Project entity, CancellationToken ct)
    {
        _context.Projects.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(Project entity, CancellationToken ct)
    {
        _context.Projects.Update(entity);
        return await _context.SaveChangesAsync(ct) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null) return false;

        _context.Projects.Remove(entity);
        return await _context.SaveChangesAsync(ct) > 0;
    }
}
