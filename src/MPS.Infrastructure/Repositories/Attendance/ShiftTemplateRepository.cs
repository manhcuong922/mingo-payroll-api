using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class ShiftTemplateRepository : IShiftTemplateRepository
{
    private readonly AppDbContext _context;

    public ShiftTemplateRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<ShiftTemplate> Items, int Total)> GetPagedAsync(
        bool? isActive,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ShiftTemplates.AsNoTracking().AsQueryable();
        if (isActive.HasValue)
            query = query.Where(t => t.IsActive == isActive.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(t => t.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<ShiftTemplate?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.ShiftTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<bool> ExistsByCodeAsync(string code, int? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim();
        var query = _context.ShiftTemplates.AsNoTracking().Where(t => t.Code == normalized);
        if (excludeId.HasValue)
            query = query.Where(t => t.Id != excludeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim();
        var query = _context.ShiftTemplates.AsNoTracking().Where(t => t.Name == normalized);
        if (excludeId.HasValue)
            query = query.Where(t => t.Id != excludeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> IsInUseAsync(int shiftTemplateId, CancellationToken cancellationToken = default) =>
        _context.EmployeeShiftAssignments
            .AsNoTracking()
            .AnyAsync(a => a.ShiftTemplateId == shiftTemplateId, cancellationToken);

    public async Task<int> AddAsync(ShiftTemplate entity, CancellationToken cancellationToken = default)
    {
        await _context.ShiftTemplates.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(ShiftTemplate entity, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.ShiftTemplates.FirstOrDefaultAsync(t => t.Id == entity.Id, cancellationToken);
        if (tracked is null) return false;

        tracked.Code = entity.Code;
        tracked.Name = entity.Name;
        tracked.StartTime = entity.StartTime;
        tracked.EndTime = entity.EndTime;
        tracked.BreakStartTime = entity.BreakStartTime;
        tracked.BreakEndTime = entity.BreakEndTime;
        tracked.StandardHours = entity.StandardHours;
        tracked.StandardDays = entity.StandardDays;
        tracked.WorkRateNormal = entity.WorkRateNormal;
        tracked.WorkRateWeeklyRest = entity.WorkRateWeeklyRest;
        tracked.WorkRateHoliday = entity.WorkRateHoliday;
        tracked.IsNightShift = entity.IsNightShift;
        tracked.AppliedAttendanceRuleId = entity.AppliedAttendanceRuleId;
        tracked.IsActive = entity.IsActive;
        tracked.Notes = entity.Notes;
        tracked.UpdatedAt = entity.UpdatedAt;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeactivateAsync(int id, int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.ShiftTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tracked is null) return false;

        tracked.IsActive = false;
        tracked.UpdatedAt = nowUtc;
        tracked.UpdatedByUserId = updatedByUserId;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> HardDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.ShiftTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tracked is null) return false;
        _context.ShiftTemplates.Remove(tracked);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
