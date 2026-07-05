using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class ProcessTemplateRepository : IProcessTemplateRepository
{
    private readonly AppDbContext _context;

    public ProcessTemplateRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ProcessTemplate>> GetAllAsync(CancellationToken ct) =>
        await _context.ProcessTemplates
            .AsNoTracking()
            .Include(t => t.DefaultDepartment)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

    public Task<ProcessTemplate?> GetByIdWithStepsAsync(int id, CancellationToken ct) =>
        _context.ProcessTemplates
            .AsNoTracking()
            .Include(t => t.DefaultDepartment)
            .Include(t => t.Steps.OrderBy(s => s.StepOrder))
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<ProcessTemplate?> GetByIdWithStepsTrackedAsync(int id, CancellationToken ct) =>
        _context.ProcessTemplates
            .Include(t => t.Steps)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<(IReadOnlyList<ProcessTemplate> Items, int Total)> GetPagedAsync(
        string? keyword, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.ProcessTemplates
            .AsNoTracking()
            .Include(t => t.DefaultDepartment)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(t => t.Code.ToLower().Contains(kw) || t.Name.ToLower().Contains(kw));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<bool> CodeExistsAsync(string code, int? excludeId, CancellationToken ct) =>
        _context.ProcessTemplates.AnyAsync(
            t => t.Code == code && (excludeId == null || t.Id != excludeId), ct);

    public async Task<int> AddAsync(ProcessTemplate entity, CancellationToken ct)
    {
        _context.ProcessTemplates.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(ProcessTemplate entity, CancellationToken ct)
    {
        _context.ProcessTemplates.Update(entity);
        return await _context.SaveChangesAsync(ct) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await _context.ProcessTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null) return false;

        _context.ProcessTemplates.Remove(entity);
        return await _context.SaveChangesAsync(ct) > 0;
    }
}
