using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class OvertimeSlotTemplateRepository : IOvertimeSlotTemplateRepository
{
    private readonly AppDbContext _context;

    public OvertimeSlotTemplateRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<OvertimeSlotTemplate> Items, int Total)> GetPagedAsync(
        bool? isActive,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        var query = _context.OvertimeSlotTemplates.AsNoTracking().AsQueryable();
        if (isActive.HasValue)
            query = query.Where(t => t.IsActive == isActive.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(t => t.StartTime)
            .ThenBy(t => t.EndTime)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<OvertimeSlotTemplate?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.OvertimeSlotTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<OvertimeSlotTemplate>> GetActiveAsync(
        CancellationToken cancellationToken = default) =>
        await _context.OvertimeSlotTemplates
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.StartTime)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsByCodeAsync(string code, int? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim();
        var query = _context.OvertimeSlotTemplates.AsNoTracking().Where(t => t.Code == normalized);
        if (excludeId.HasValue)
            query = query.Where(t => t.Id != excludeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public async Task<int> AddAsync(OvertimeSlotTemplate entity, CancellationToken cancellationToken = default)
    {
        await _context.OvertimeSlotTemplates.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(OvertimeSlotTemplate entity, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.OvertimeSlotTemplates.FirstOrDefaultAsync(t => t.Id == entity.Id, cancellationToken);
        if (tracked is null) return false;

        tracked.Code = entity.Code;
        tracked.StartTime = entity.StartTime;
        tracked.EndTime = entity.EndTime;
        tracked.IsNightSlot = entity.IsNightSlot;
        tracked.MultiplierNormalDay = entity.MultiplierNormalDay;
        tracked.MultiplierWeeklyRestDay = entity.MultiplierWeeklyRestDay;
        tracked.MultiplierHolidayDay = entity.MultiplierHolidayDay;
        tracked.MultiplierNormalNightNoPrevOt = entity.MultiplierNormalNightNoPrevOt;
        tracked.MultiplierNormalNightWithPrevOt = entity.MultiplierNormalNightWithPrevOt;
        tracked.MultiplierWeeklyRestNight = entity.MultiplierWeeklyRestNight;
        tracked.MultiplierHolidayNight = entity.MultiplierHolidayNight;
        tracked.Description = entity.Description;
        tracked.IsActive = entity.IsActive;
        tracked.UpdatedAt = entity.UpdatedAt;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeactivateAsync(int id, int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.OvertimeSlotTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tracked is null) return false;

        tracked.IsActive = false;
        tracked.UpdatedAt = nowUtc;
        tracked.UpdatedByUserId = updatedByUserId;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> IsReferencedAsync(int id, CancellationToken cancellationToken = default) =>
        _context.OvertimeRequests.AsNoTracking().AnyAsync(r => r.OvertimeSlotTemplateId == id, cancellationToken);

    public async Task<IReadOnlyList<string>> DeactivateOverlappingActiveAsync(
        TimeOnly startTime,
        TimeOnly endTime,
        int? excludeId,
        int updatedByUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        // Load candidate = active slot (trừ chính mình nếu là update). Overlap check phải làm
        // in-memory vì logic cross-midnight khó biểu diễn trong SQL sạch. Slot ít (<20 rows) nên OK.
        var query = _context.OvertimeSlotTemplates.Where(t => t.IsActive);
        if (excludeId.HasValue) query = query.Where(t => t.Id != excludeId.Value);
        var candidates = await query.ToListAsync(cancellationToken);

        var overlapping = candidates
            .Where(c => TimeOfDayOverlap.Overlaps(startTime, endTime, c.StartTime, c.EndTime))
            .ToList();
        if (overlapping.Count == 0) return Array.Empty<string>();

        foreach (var slot in overlapping)
        {
            slot.IsActive = false;
            slot.UpdatedAt = nowUtc;
            slot.UpdatedByUserId = updatedByUserId;
        }
        await _context.SaveChangesAsync(cancellationToken);
        return overlapping.Select(s => s.Code).ToList();
    }
}

/// <summary>
/// Overlap check cho slot template (khung giờ recurring daily). Xử lý cross-midnight
/// bằng cách phân slot thành 1-2 interval trong [0, 24h) rồi so pairwise.
/// </summary>
internal static class TimeOfDayOverlap
{
    public static bool Overlaps(TimeOnly aStart, TimeOnly aEnd, TimeOnly bStart, TimeOnly bEnd)
    {
        var aIntervals = ToIntervals(aStart, aEnd);
        var bIntervals = ToIntervals(bStart, bEnd);
        foreach (var (aS, aE) in aIntervals)
        {
            foreach (var (bS, bE) in bIntervals)
            {
                if (aS < bE && bS < aE) return true;
            }
        }
        return false;
    }

    private static IReadOnlyList<(long Start, long End)> ToIntervals(TimeOnly start, TimeOnly end)
    {
        // Non-crossing: [start, end)
        if (end > start) return new[] { (start.Ticks, end.Ticks) };
        // Cross-midnight: [start, 24h) ∪ [0, end)
        return new[]
        {
            (start.Ticks, TimeSpan.TicksPerDay),
            (0L, end.Ticks),
        };
    }
}
