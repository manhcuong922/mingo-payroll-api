using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class LeaveTypeRepository : ILeaveTypeRepository
{
    private readonly AppDbContext _context;

    public LeaveTypeRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<LeaveType> Items, int Total)> GetPagedAsync(
        bool? isActive,
        string? search,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LeaveTypes.AsNoTracking().AsQueryable();

        if (isActive.HasValue)
            query = query.Where(t => t.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(t => EF.Functions.Like(t.Code, $"%{s}%")
                || EF.Functions.Like(t.Name, $"%{s}%"));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(t => t.Code)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<LeaveType?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.LeaveTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<LeaveType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _context.LeaveTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Code == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<LeaveType>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.LeaveTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Code)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsByCodeAsync(string code, int? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var query = _context.LeaveTypes.AsNoTracking().Where(t => t.Code == normalized);
        if (excludeId.HasValue)
            query = query.Where(t => t.Id != excludeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim();
        var query = _context.LeaveTypes.AsNoTracking().Where(t => t.Name == normalized);
        if (excludeId.HasValue)
            query = query.Where(t => t.Id != excludeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public async Task<int> AddAsync(LeaveType entity, CancellationToken cancellationToken = default)
    {
        await _context.LeaveTypes.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(LeaveType entity, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.LeaveTypes.FirstOrDefaultAsync(t => t.Id == entity.Id, cancellationToken);
        if (tracked is null) return false;

        tracked.Code = entity.Code;
        tracked.Name = entity.Name;
        tracked.Description = entity.Description;
        tracked.IsPaid = entity.IsPaid;
        tracked.PaidRate = entity.PaidRate;
        tracked.MaxDaysPerRequest = entity.MaxDaysPerRequest;
        tracked.AllowEmployeeSelection = entity.AllowEmployeeSelection;
        tracked.MaxDaysPerYear = entity.MaxDaysPerYear;
        tracked.MaxDaysPerMonth = entity.MaxDaysPerMonth;
        tracked.MaxDaysPerWeek = entity.MaxDaysPerWeek;
        tracked.ApplyScope = entity.ApplyScope;
        tracked.TargetDepartmentIds = entity.TargetDepartmentIds;
        tracked.TargetEmployeeIds = entity.TargetEmployeeIds;
        tracked.RequireDocument = entity.RequireDocument;
        tracked.DeductsBalance = entity.DeductsBalance;
        tracked.MinNoticeDaysForShort = entity.MinNoticeDaysForShort;
        tracked.MinNoticeDaysForMedium = entity.MinNoticeDaysForMedium;
        tracked.MinNoticeDaysForLong = entity.MinNoticeDaysForLong;
        tracked.AutoFallbackToUnpaid = entity.AutoFallbackToUnpaid;
        tracked.AllowRetroactive = entity.AllowRetroactive;
        tracked.IsActive = entity.IsActive;
        tracked.UpdatedAt = entity.UpdatedAt;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeactivateAsync(int id, int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.LeaveTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tracked is null) return false;

        tracked.IsActive = false;
        tracked.UpdatedAt = nowUtc;
        tracked.UpdatedByUserId = updatedByUserId;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

}
