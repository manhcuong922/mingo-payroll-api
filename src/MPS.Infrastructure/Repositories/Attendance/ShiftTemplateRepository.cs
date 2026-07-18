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
        var query = _context.ShiftTemplates
            .AsNoTracking()
            .Include(t => t.AppliedAttendanceRule)
            .AsQueryable();
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
        _context.ShiftTemplates
            .AsNoTracking()
            .Include(t => t.AppliedAttendanceRule)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<string, int>> GetActiveIdsByCodesAsync(
        IEnumerable<string> codes,
        CancellationToken cancellationToken = default)
    {
        var distinct = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (distinct.Length == 0)
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var rows = await _context.ShiftTemplates
            .AsNoTracking()
            .Where(t => t.IsActive && distinct.Contains(t.Code))
            .Select(t => new { t.Code, t.Id })
            .ToListAsync(cancellationToken);

        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
            map[row.Code] = row.Id;
        return map;
    }

    public Task<bool> ExistsByCodeAsync(string code, int? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim();
        var query = _context.ShiftTemplates
            .AsNoTracking()
            .Where(t => t.IsActive && t.Code == normalized);
        if (excludeId.HasValue)
            query = query.Where(t => t.Id != excludeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim();
        var query = _context.ShiftTemplates
            .AsNoTracking()
            .Where(t => t.IsActive && t.Name == normalized);
        if (excludeId.HasValue)
            query = query.Where(t => t.Id != excludeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public async Task<ShiftTemplateFutureUsage> GetFutureUsageAsync(int shiftTemplateId, DateOnly fromDate, CancellationToken cancellationToken = default)
    {
        var futureAssignments = _context.EmployeeShiftAssignments
            .AsNoTracking()
            .Where(a => a.ShiftTemplateId == shiftTemplateId && a.WorkDate >= fromDate);

        var assignmentCount = await futureAssignments.CountAsync(cancellationToken);
        DateOnly? earliest = assignmentCount > 0
            ? await futureAssignments.MinAsync(a => a.WorkDate, cancellationToken)
            : null;

        var legacyRefs = await _context.ScheduleTemplates
            .AsNoTracking()
            .Where(s => s.ShiftTemplateId == shiftTemplateId
                     && s.IsActive
                     && s.EffectiveTo >= fromDate)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(cancellationToken);

        var multiRefs = await _context.ScheduleTemplateShifts
            .AsNoTracking()
            .Where(sts => sts.ShiftTemplateId == shiftTemplateId
                       && sts.ScheduleTemplate.IsActive
                       && sts.ScheduleTemplate.EffectiveTo >= fromDate)
            .Select(sts => new { sts.ScheduleTemplate.Id, sts.ScheduleTemplate.Name })
            .ToListAsync(cancellationToken);

        var scheduleTemplateNames = legacyRefs
            .Concat(multiRefs)
            .GroupBy(x => x.Id)
            .Select(g => g.First().Name)
            .OrderBy(n => n, StringComparer.CurrentCulture)
            .ToList();

        return new ShiftTemplateFutureUsage(assignmentCount, earliest, scheduleTemplateNames);
    }

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

}
