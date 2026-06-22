using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class ScheduleTemplateRepository : IScheduleTemplateRepository
{
    private readonly AppDbContext _context;

    public ScheduleTemplateRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<ScheduleTemplate> Items, int Total)> GetPagedAsync(
        bool? isActive,
        string? keyword,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ScheduleTemplates.AsNoTracking()
            .Include(t => t.ShiftTemplate)
            .Include(t => t.Employees)
                .ThenInclude(e => e.EmployeeInformation)
                .ThenInclude(e => e.User)
            .AsQueryable();

        if (isActive is not null)
        {
            query = query.Where(t => t.IsActive == isActive);
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(t => EF.Functions.Like(t.Name, $"%{kw}%"));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<ScheduleTemplate?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.ScheduleTemplates
            .AsNoTracking()
            .Include(t => t.ShiftTemplate)
            .Include(t => t.Employees)
                .ThenInclude(e => e.EmployeeInformation)
                .ThenInclude(e => e.User)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<bool> ExistsByNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default) =>
        _context.ScheduleTemplates
            .AsNoTracking()
            .AnyAsync(t => t.Name == name && (excludeId == null || t.Id != excludeId), cancellationToken);

    public async Task<int> AddAsync(
        ScheduleTemplate entity,
        IReadOnlyCollection<int> employeeInformationIds,
        CancellationToken cancellationToken = default)
    {
        entity.Employees = employeeInformationIds
            .Distinct()
            .Select(empId => new ScheduleTemplateEmployee { EmployeeInformationId = empId })
            .ToList();

        await _context.ScheduleTemplates.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(
        ScheduleTemplate entity,
        IReadOnlyCollection<int> employeeInformationIds,
        CancellationToken cancellationToken = default)
    {
        var tracked = await _context.ScheduleTemplates
            .Include(t => t.Employees)
            .FirstOrDefaultAsync(t => t.Id == entity.Id, cancellationToken)
            ?? throw new InvalidOperationException($"ScheduleTemplate {entity.Id} not found.");

        tracked.Name = entity.Name;
        tracked.ShiftTemplateId = entity.ShiftTemplateId;
        tracked.EffectiveFrom = entity.EffectiveFrom;
        tracked.EffectiveTo = entity.EffectiveTo;
        tracked.RepeatUnit = entity.RepeatUnit;
        tracked.RepeatEvery = entity.RepeatEvery;
        tracked.DaysOfWeekMask = entity.DaysOfWeekMask;
        tracked.DaysOfMonthMask = entity.DaysOfMonthMask;
        tracked.IsActive = entity.IsActive;
        tracked.Notes = entity.Notes;
        tracked.UpdatedAt = entity.UpdatedAt;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        var desired = employeeInformationIds.Distinct().ToHashSet();
        var existing = tracked.Employees.ToDictionary(e => e.EmployeeInformationId);

        foreach (var empId in desired)
        {
            if (!existing.ContainsKey(empId))
            {
                tracked.Employees.Add(new ScheduleTemplateEmployee { EmployeeInformationId = empId });
            }
        }
        foreach (var (empId, link) in existing)
        {
            if (!desired.Contains(empId))
            {
                _context.ScheduleTemplateEmployees.Remove(link);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeactivateAsync(int id, int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.ScheduleTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tracked is null) return false;

        tracked.IsActive = false;
        tracked.UpdatedAt = nowUtc;
        tracked.UpdatedByUserId = updatedByUserId;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<(int TemplateId, string TemplateName, int EmployeeInformationId, string EmployeeFullName)>>
        GetOverlappingActiveTemplatesAsync(
            IReadOnlyCollection<int> employeeIds,
            DateOnly fromDate,
            DateOnly toDate,
            int? excludeTemplateId,
            CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
        {
            return Array.Empty<(int, string, int, string)>();
        }

        var rows = await _context.ScheduleTemplateEmployees
            .AsNoTracking()
            .Where(ste => employeeIds.Contains(ste.EmployeeInformationId)
                && ste.ScheduleTemplate.IsActive
                && ste.ScheduleTemplate.EffectiveFrom <= toDate
                && ste.ScheduleTemplate.EffectiveTo >= fromDate
                && (excludeTemplateId == null || ste.ScheduleTemplateId != excludeTemplateId))
            .Select(ste => new
            {
                TemplateId = ste.ScheduleTemplateId,
                TemplateName = ste.ScheduleTemplate.Name,
                ste.EmployeeInformationId,
                EmployeeFullName = ste.EmployeeInformation.FullName
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => (r.TemplateId, r.TemplateName, r.EmployeeInformationId, r.EmployeeFullName))
            .ToList();
    }
}
