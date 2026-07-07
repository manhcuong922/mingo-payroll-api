using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class WeeklyDayOffRuleRepository : IWeeklyDayOffRuleRepository
{
    private readonly AppDbContext _context;

    public WeeklyDayOffRuleRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<WeeklyDayOffRule>> ListAsync(
        bool includeInactive,
        string? applyScope,
        DateOnly? effectiveOn,
        CancellationToken cancellationToken = default)
    {
        var query = _context.WeeklyDayOffRules.AsNoTracking().AsQueryable();

        if (!includeInactive)
            query = query.Where(r => r.IsActive);

        if (!string.IsNullOrWhiteSpace(applyScope))
            query = query.Where(r => r.ApplyScope == applyScope);

        if (effectiveOn.HasValue)
        {
            var date = effectiveOn.Value;
            query = query.Where(r => r.EffectiveFrom <= date && (r.EffectiveTo == null || r.EffectiveTo >= date));
        }

        return await query
            .OrderByDescending(r => r.IsActive)
            .ThenBy(r => r.EffectiveFrom)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<WeeklyDayOffRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.WeeklyDayOffRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> ExistsByNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default)
    {
        // Chỉ check trùng tên trong các rule đang active — rule đã soft-delete được phép
        // dùng lại tên (VD user xóa rule mặc định rồi tạo lại với cùng tên).
        var query = _context.WeeklyDayOffRules.AsNoTracking()
            .Where(r => r.IsActive && r.Name == name);
        if (excludeId.HasValue)
            query = query.Where(r => r.Id != excludeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WeeklyDayOffRule>> FindActiveOnDateAsync(
        DateOnly date,
        CancellationToken cancellationToken = default) =>
        await _context.WeeklyDayOffRules.AsNoTracking()
            .Where(r => r.IsActive
                     && r.EffectiveFrom <= date
                     && (r.EffectiveTo == null || r.EffectiveTo >= date))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WeeklyDayOffRule>> FindActiveInRangeAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default) =>
        await _context.WeeklyDayOffRules.AsNoTracking()
            .Where(r => r.IsActive
                     && r.EffectiveFrom <= toDate
                     && (r.EffectiveTo == null || r.EffectiveTo >= fromDate))
            .ToListAsync(cancellationToken);

    public async Task<int> AddAsync(WeeklyDayOffRule entity, CancellationToken cancellationToken = default)
    {
        await _context.WeeklyDayOffRules.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(WeeklyDayOffRule entity, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.WeeklyDayOffRules.FirstOrDefaultAsync(r => r.Id == entity.Id, cancellationToken);
        if (tracked is null) return false;

        tracked.Name = entity.Name;
        tracked.Description = entity.Description;
        tracked.DaysOfWeekMask = entity.DaysOfWeekMask;
        tracked.ApplyScope = entity.ApplyScope;
        tracked.TargetDepartmentIds = entity.TargetDepartmentIds;
        tracked.TargetEmployeeIds = entity.TargetEmployeeIds;
        tracked.EffectiveFrom = entity.EffectiveFrom;
        tracked.EffectiveTo = entity.EffectiveTo;
        tracked.IsActive = entity.IsActive;
        tracked.UpdatedAt = entity.UpdatedAt;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeactivateAsync(int id, int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.WeeklyDayOffRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (tracked is null) return false;

        tracked.IsActive = false;
        tracked.UpdatedAt = nowUtc;
        tracked.UpdatedByUserId = updatedByUserId;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
