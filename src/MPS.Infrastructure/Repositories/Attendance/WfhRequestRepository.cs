using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class WfhRequestRepository : IWfhRequestRepository
{
    private readonly AppDbContext _context;

    public WfhRequestRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<WfhRequest> Items, int Total)> GetPagedAsync(
        int? employeeInformationId,
        WfhRequestStatus? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int size,
        int? departmentIdFilter = null,
        OvertimeApproverRoute? approverRouteFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.WfhRequests
            .AsNoTracking()
            .Include(r => r.EmployeeInformation)
            .AsQueryable();

        if (employeeInformationId.HasValue)
            query = query.Where(r => r.EmployeeInformationId == employeeInformationId.Value);
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);
        if (fromDate.HasValue)
            query = query.Where(r => r.EndDate >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(r => r.StartDate <= toDate.Value);
        if (departmentIdFilter.HasValue)
            query = query.Where(r => r.EmployeeInformation.DepartmentId == departmentIdFilter.Value);
        if (approverRouteFilter.HasValue)
            query = query.Where(r => r.ApproverRoute == approverRouteFilter.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.StartDate)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<WfhRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.WfhRequests
            .AsNoTracking()
            .Include(r => r.EmployeeInformation)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<int> AddAsync(WfhRequest entity, CancellationToken cancellationToken = default)
    {
        await _context.WfhRequests.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(WfhRequest entity, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.WfhRequests.FirstOrDefaultAsync(r => r.Id == entity.Id, cancellationToken);
        if (tracked is null) return false;

        tracked.StartDate = entity.StartDate;
        tracked.EndDate = entity.EndDate;
        tracked.TotalDays = entity.TotalDays;
        tracked.WorkPlan = entity.WorkPlan;
        tracked.ApproverRoute = entity.ApproverRoute;
        tracked.Status = entity.Status;
        tracked.Reason = entity.Reason;
        tracked.ReviewNote = entity.ReviewNote;
        tracked.ReviewedByUserId = entity.ReviewedByUserId;
        tracked.ReviewedAt = entity.ReviewedAt;
        tracked.CancelledAt = entity.CancelledAt;
        tracked.CancelledByUserId = entity.CancelledByUserId;
        tracked.CancelReason = entity.CancelReason;
        tracked.UpdatedAt = entity.UpdatedAt;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<decimal> SumApprovedDaysInMonthAsync(
        int employeeInformationId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var approved = await _context.WfhRequests
            .AsNoTracking()
            .Where(r => r.EmployeeInformationId == employeeInformationId
                && r.Status == WfhRequestStatus.Approved
                && r.StartDate <= monthEnd
                && r.EndDate >= monthStart)
            .Select(r => new { r.StartDate, r.EndDate, r.TotalDays })
            .ToListAsync(cancellationToken);

        decimal total = 0m;
        foreach (var r in approved)
        {
            var start = r.StartDate > monthStart ? r.StartDate : monthStart;
            var end = r.EndDate < monthEnd ? r.EndDate : monthEnd;
            var rangeDays = end.DayNumber - start.DayNumber + 1;
            var origDays = r.EndDate.DayNumber - r.StartDate.DayNumber + 1;
            if (origDays <= 0) continue;
            total += r.TotalDays * ((decimal)rangeDays / origDays);
        }
        return total;
    }

    public Task<bool> HasOverlappingWfhAsync(
        int employeeInformationId,
        DateOnly startDate,
        DateOnly endDate,
        int? excludeRequestId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.WfhRequests
            .AsNoTracking()
            .Where(r => r.EmployeeInformationId == employeeInformationId
                && (r.Status == WfhRequestStatus.Pending || r.Status == WfhRequestStatus.Approved)
                && r.StartDate <= endDate
                && r.EndDate >= startDate);
        if (excludeRequestId.HasValue)
            query = query.Where(r => r.Id != excludeRequestId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> HasOverlappingLeaveAsync(
        int employeeInformationId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        return _context.LeaveRequests
            .AsNoTracking()
            .AnyAsync(r => r.EmployeeInformationId == employeeInformationId
                && r.Status == LeaveRequestStatus.Approved
                && r.StartDate <= endDate
                && r.EndDate >= startDate, cancellationToken);
    }
}
