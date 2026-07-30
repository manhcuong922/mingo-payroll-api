using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class PublicHolidayRepository : IPublicHolidayRepository
{
    private readonly AppDbContext _context;

    public PublicHolidayRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PublicHoliday>> GetByYearAsync(int year, bool includeInactive, CancellationToken cancellationToken = default)
    {
        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year, 12, 31);
        var query = _context.PublicHolidays
            .AsNoTracking()
            .Where(h => h.HolidayDate <= end && h.HolidayEndDate >= start);

        if (!includeInactive)
            query = query.Where(h => h.IsActive);

        return await query
            .OrderBy(h => h.HolidayDate)
            .ThenBy(h => h.HolidayEndDate)
            .ToListAsync(cancellationToken);
    }

    public Task<PublicHoliday?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.PublicHolidays.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

    public Task<bool> IsHolidayAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        _context.PublicHolidays.AsNoTracking()
            .AnyAsync(h => h.HolidayDate <= date && h.HolidayEndDate >= date && h.IsActive, cancellationToken);

    public Task<bool> ExistsOverlapAsync(DateOnly startDate, DateOnly endDate, int? excludeId, CancellationToken cancellationToken = default)
    {
        var query = _context.PublicHolidays.AsNoTracking()
            .Where(h => h.HolidayDate <= endDate
                     && h.HolidayEndDate >= startDate
                     && h.IsActive);

        if (excludeId.HasValue)
            query = query.Where(h => h.Id != excludeId.Value);

        return query.AnyAsync(cancellationToken);
    }

    public async Task<int> AddAsync(PublicHoliday entity, CancellationToken cancellationToken = default)
    {
        await _context.PublicHolidays.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(PublicHoliday entity, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.PublicHolidays.FirstOrDefaultAsync(h => h.Id == entity.Id, cancellationToken);
        if (tracked is null) return false;

        tracked.Name = entity.Name;
        tracked.HolidayDate = entity.HolidayDate;
        tracked.HolidayEndDate = entity.HolidayEndDate;
        tracked.Description = entity.Description;
        tracked.IsActive = entity.IsActive;
        tracked.UpdatedAt = entity.UpdatedAt;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeactivateAsync(int id, int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.PublicHolidays.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (tracked is null) return false;

        tracked.IsActive = false;
        tracked.UpdatedAt = nowUtc;
        tracked.UpdatedByUserId = updatedByUserId;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
