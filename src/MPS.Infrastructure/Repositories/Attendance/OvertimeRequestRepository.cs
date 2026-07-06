using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class OvertimeRequestRepository : IOvertimeRequestRepository
{
    private readonly AppDbContext _context;

    public OvertimeRequestRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<OvertimeRequest> Items, int Total)> GetPagedAsync(
        int? employeeInformationId,
        OtRequestStatus? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        var query = _context.OvertimeRequests
            .AsNoTracking()
            .Include(r => r.EmployeeInformation)
            .Include(r => r.OvertimeSlotTemplate)
            .AsQueryable();

        if (employeeInformationId.HasValue)
            query = query.Where(r => r.EmployeeInformationId == employeeInformationId.Value);
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);
        if (fromDate.HasValue)
            query = query.Where(r => r.WorkDate >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(r => r.WorkDate <= toDate.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.WorkDate)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<OvertimeRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.OvertimeRequests
            .AsNoTracking()
            .Include(r => r.EmployeeInformation)
            .Include(r => r.OvertimeSlotTemplate)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<decimal> GetTotalOtHoursInMonthAsync(int employeeInformationId, int year, int month, CancellationToken cancellationToken = default)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        return await _context.OvertimeRequests
            .AsNoTracking()
            .Where(r => r.EmployeeInformationId == employeeInformationId
                && r.Status == OtRequestStatus.Approved
                && r.WorkDate >= start
                && r.WorkDate < end)
            .SumAsync(r => (decimal?)r.OtHours, cancellationToken) ?? 0m;
    }

    public async Task<decimal> GetTotalOtHoursInYearAsync(int employeeInformationId, int year, CancellationToken cancellationToken = default)
    {
        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year + 1, 1, 1);
        return await _context.OvertimeRequests
            .AsNoTracking()
            .Where(r => r.EmployeeInformationId == employeeInformationId
                && r.Status == OtRequestStatus.Approved
                && r.WorkDate >= start
                && r.WorkDate < end)
            .SumAsync(r => (decimal?)r.OtHours, cancellationToken) ?? 0m;
    }

    public async Task<int> AddAsync(OvertimeRequest entity, CancellationToken cancellationToken = default)
    {
        await _context.OvertimeRequests.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(OvertimeRequest entity, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.OvertimeRequests.FirstOrDefaultAsync(r => r.Id == entity.Id, cancellationToken);
        if (tracked is null) return false;

        tracked.Status = entity.Status;
        tracked.OtDayHours = entity.OtDayHours;
        tracked.OtNightHours = entity.OtNightHours;
        tracked.ReviewNote = entity.ReviewNote;
        tracked.ReviewedByUserId = entity.ReviewedByUserId;
        tracked.ReviewedAt = entity.ReviewedAt;
        tracked.UpdatedAt = entity.UpdatedAt;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}