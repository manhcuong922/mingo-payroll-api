using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class AttendanceRuleRepository : IAttendanceRuleRepository
{
    private readonly AppDbContext _context;

    public AttendanceRuleRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<AttendanceRule> Items, int Total)> GetPagedAsync(
        bool? isActive,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AttendanceRules.AsNoTracking().AsQueryable();
        if (isActive.HasValue)
            query = query.Where(r => r.IsActive == isActive.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(r => r.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<AttendanceRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.AttendanceRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> ExistsByNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim();
        var query = _context.AttendanceRules.AsNoTracking().Where(r => r.Name == normalized);
        if (excludeId.HasValue)
            query = query.Where(r => r.Id != excludeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetShiftTemplateIdsUsingRuleAsync(int ruleId, CancellationToken cancellationToken = default)
    {
        return await _context.ShiftTemplates.AsNoTracking()
            .Where(s => s.AppliedAttendanceRuleId == ruleId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> AddAsync(AttendanceRule entity, CancellationToken cancellationToken = default)
    {
        await _context.AttendanceRules.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(AttendanceRule entity, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.AttendanceRules.FirstOrDefaultAsync(r => r.Id == entity.Id, cancellationToken);
        if (tracked is null) return false;

        tracked.Name = entity.Name;
        tracked.Description = entity.Description;
        tracked.WorkRateNormal = entity.WorkRateNormal;
        tracked.WorkRateWeeklyRest = entity.WorkRateWeeklyRest;
        tracked.WorkRateHoliday = entity.WorkRateHoliday;
        tracked.WorkRateNight = entity.WorkRateNight;
        tracked.IsActive = entity.IsActive;
        tracked.UpdatedAt = entity.UpdatedAt;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeactivateAsync(int id, int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.AttendanceRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (tracked is null) return false;

        tracked.IsActive = false;
        tracked.UpdatedAt = nowUtc;
        tracked.UpdatedByUserId = updatedByUserId;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

}
