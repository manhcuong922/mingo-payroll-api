using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class LateEarlyRequestRepository : ILateEarlyRequestRepository
{
    private readonly AppDbContext _context;

    public LateEarlyRequestRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<LateEarlyRequest> Items, int Total)> GetPagedAsync(
        int? employeeInformationId,
        LateEarlyRequestStatus? status,
        LateEarlyKind? kind,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int size,
        int? departmentIdFilter = null,
        OvertimeApproverRoute? approverRouteFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LateEarlyRequests
            .AsNoTracking()
            .Include(r => r.EmployeeInformation)
            .AsQueryable();

        if (employeeInformationId.HasValue)
            query = query.Where(r => r.EmployeeInformationId == employeeInformationId.Value);
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);
        if (kind.HasValue)
            query = query.Where(r => r.Kind == kind.Value);
        if (fromDate.HasValue)
            query = query.Where(r => r.WorkDate >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(r => r.WorkDate <= toDate.Value);
        if (departmentIdFilter.HasValue)
            query = query.Where(r => r.EmployeeInformation.DepartmentId == departmentIdFilter.Value);
        if (approverRouteFilter.HasValue)
            query = query.Where(r => r.ApproverRoute == approverRouteFilter.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.WorkDate)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<LateEarlyRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.LateEarlyRequests
            .AsNoTracking()
            .Include(r => r.EmployeeInformation)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<int> AddAsync(LateEarlyRequest entity, CancellationToken cancellationToken = default)
    {
        await _context.LateEarlyRequests.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(LateEarlyRequest entity, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.LateEarlyRequests.FirstOrDefaultAsync(r => r.Id == entity.Id, cancellationToken);
        if (tracked is null) return false;

        tracked.WorkDate = entity.WorkDate;
        tracked.Kind = entity.Kind;
        tracked.Minutes = entity.Minutes;
        tracked.IsUnplanned = entity.IsUnplanned;
        tracked.DeductFromLeaveBalance = entity.DeductFromLeaveBalance;
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

    public async Task<int> CountApprovedInMonthAsync(
        int employeeInformationId,
        int year,
        int month,
        LateEarlyKind kind,
        CancellationToken cancellationToken = default)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        return await _context.LateEarlyRequests
            .AsNoTracking()
            .Where(r => r.EmployeeInformationId == employeeInformationId
                && r.Status == LateEarlyRequestStatus.Approved
                && r.Kind == kind
                && r.WorkDate >= start
                && r.WorkDate < end)
            .CountAsync(cancellationToken);
    }

    public Task<bool> HasOverlapAsync(
        int employeeInformationId,
        DateOnly workDate,
        LateEarlyKind kind,
        int? excludeRequestId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LateEarlyRequests
            .AsNoTracking()
            .Where(r => r.EmployeeInformationId == employeeInformationId
                && r.WorkDate == workDate
                && r.Kind == kind
                && (r.Status == LateEarlyRequestStatus.Pending
                    || r.Status == LateEarlyRequestStatus.Approved));
        if (excludeRequestId.HasValue)
            query = query.Where(r => r.Id != excludeRequestId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LateEarlyRequest>> GetApprovedByEmployeeAndDateAsync(
        int employeeInformationId,
        DateOnly workDate,
        CancellationToken cancellationToken = default) =>
        await _context.LateEarlyRequests
            .AsNoTracking()
            .Where(r => r.EmployeeInformationId == employeeInformationId
                && r.WorkDate == workDate
                && r.Status == LateEarlyRequestStatus.Approved)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LateEarlyRequest>> GetApprovedByEmployeesAndRangeAsync(
        IReadOnlyList<int> employeeInformationIds,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (employeeInformationIds.Count == 0) return Array.Empty<LateEarlyRequest>();

        return await _context.LateEarlyRequests
            .AsNoTracking()
            .Where(r => employeeInformationIds.Contains(r.EmployeeInformationId)
                && r.WorkDate >= fromDate
                && r.WorkDate <= toDate
                && r.Status == LateEarlyRequestStatus.Approved)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasPendingOrApprovedInRangeAsync(
        int employeeInformationId,
        DateOnly startDate,
        DateOnly endDate,
        int? excludeRequestId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LateEarlyRequests
            .AsNoTracking()
            .Where(r => r.EmployeeInformationId == employeeInformationId
                && (r.Status == LateEarlyRequestStatus.Pending
                    || r.Status == LateEarlyRequestStatus.Approved)
                && r.WorkDate >= startDate
                && r.WorkDate <= endDate);
        if (excludeRequestId.HasValue)
            query = query.Where(r => r.Id != excludeRequestId.Value);
        return query.AnyAsync(cancellationToken);
    }
}
