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
        int? createdByUserId,
        int? projectId,
        int? projectResponsibleUserId,
        int page,
        int size,
        OvertimeApproverRoute? approverRoute = null,
        int? projectDepartmentId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.OvertimeRequests
            .AsNoTracking()
            .Include(r => r.EmployeeInformation)
            .Include(r => r.Project)
            .AsQueryable();

        if (employeeInformationId.HasValue)
            query = query.Where(r => r.EmployeeInformationId == employeeInformationId.Value);
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);
        if (fromDate.HasValue)
            query = query.Where(r => r.WorkDate >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(r => r.WorkDate <= toDate.Value);
        if (createdByUserId.HasValue)
            query = query.Where(r => r.CreatedByUserId == createdByUserId.Value);
        if (projectId.HasValue)
            query = query.Where(r => r.ProjectId == projectId.Value);
        if (projectResponsibleUserId.HasValue)
            query = query.Where(r => r.Project != null && r.Project.ResponsibleUserId == projectResponsibleUserId.Value);
        if (approverRoute.HasValue)
            query = query.Where(r => r.ApproverRoute == approverRoute.Value);
        if (projectDepartmentId.HasValue)
            query = query.Where(r => r.Project != null && r.Project.DepartmentId == projectDepartmentId.Value);

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
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<decimal> GetTotalOtHoursInMonthAsync(
        int employeeInformationId,
        int year,
        int month,
        IReadOnlyCollection<OtRequestStatus>? statuses = null,
        CancellationToken cancellationToken = default)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        var effectiveStatuses = statuses ?? new[] { OtRequestStatus.Approved };
        return await _context.OvertimeRequests
            .AsNoTracking()
            .Where(r => r.EmployeeInformationId == employeeInformationId
                && effectiveStatuses.Contains(r.Status)
                && r.WorkDate >= start
                && r.WorkDate < end)
            .SumAsync(r => (decimal?)r.OtHours, cancellationToken) ?? 0m;
    }

    public async Task<decimal> GetTotalOtHoursInYearAsync(
        int employeeInformationId,
        int year,
        IReadOnlyCollection<OtRequestStatus>? statuses = null,
        CancellationToken cancellationToken = default)
    {
        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year + 1, 1, 1);
        var effectiveStatuses = statuses ?? new[] { OtRequestStatus.Approved };
        return await _context.OvertimeRequests
            .AsNoTracking()
            .Where(r => r.EmployeeInformationId == employeeInformationId
                && effectiveStatuses.Contains(r.Status)
                && r.WorkDate >= start
                && r.WorkDate < end)
            .SumAsync(r => (decimal?)r.OtHours, cancellationToken) ?? 0m;
    }

    public async Task<decimal> GetTotalOtHoursOnDateAsync(
        int employeeInformationId,
        DateOnly workDate,
        IReadOnlyCollection<OtRequestStatus> statuses,
        int? excludeId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.OvertimeRequests
            .AsNoTracking()
            .Where(r => r.EmployeeInformationId == employeeInformationId
                && r.WorkDate == workDate
                && statuses.Contains(r.Status));

        if (excludeId.HasValue)
            query = query.Where(r => r.Id != excludeId.Value);

        return await query.SumAsync(r => (decimal?)r.OtHours, cancellationToken) ?? 0m;
    }

    public async Task<IReadOnlyList<decimal>> GetMonthlyOtHoursInYearAsync(
        int employeeInformationId,
        int year,
        IReadOnlyCollection<OtRequestStatus> statuses,
        CancellationToken cancellationToken = default)
    {
        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year + 1, 1, 1);
        var grouped = await _context.OvertimeRequests
            .AsNoTracking()
            .Where(r => r.EmployeeInformationId == employeeInformationId
                && statuses.Contains(r.Status)
                && r.WorkDate >= start
                && r.WorkDate < end)
            .GroupBy(r => r.WorkDate.Month)
            .Select(g => new { Month = g.Key, Hours = g.Sum(r => r.OtHours) })
            .ToListAsync(cancellationToken);

        var result = new decimal[12];
        foreach (var row in grouped)
        {
            result[row.Month - 1] = row.Hours;
        }
        return result;
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

        // PM sửa đơn Pending có thể đổi: WorkDate/OtStartTime/OtEndTime/OtHours/Reason/ApproverRoute.
        // Approve/Reject/Cancel không mutate các field này → entity.X == DB.X → no-op an toàn.
        tracked.WorkDate = entity.WorkDate;
        tracked.OtStartTime = entity.OtStartTime;
        tracked.OtEndTime = entity.OtEndTime;
        tracked.OtHours = entity.OtHours;
        tracked.Reason = entity.Reason;
        tracked.ApproverRoute = entity.ApproverRoute;
        tracked.Status = entity.Status;
        tracked.OtDayHours = entity.OtDayHours;
        tracked.OtNightHours = entity.OtNightHours;
        tracked.ReviewNote = entity.ReviewNote;
        tracked.ReviewedByUserId = entity.ReviewedByUserId;
        tracked.ReviewedAt = entity.ReviewedAt;
        tracked.PmReviewedByUserId = entity.PmReviewedByUserId;
        tracked.PmReviewedAt = entity.PmReviewedAt;
        tracked.PmReviewNote = entity.PmReviewNote;
        tracked.ApproverReviewedByUserId = entity.ApproverReviewedByUserId;
        tracked.ApproverReviewedAt = entity.ApproverReviewedAt;
        tracked.ApproverReviewNote = entity.ApproverReviewNote;
        tracked.UpdatedAt = entity.UpdatedAt;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<OvertimeRequest>> GetActiveOnDateForEmployeeAsync(
        int employeeInformationId,
        DateOnly workDate,
        int? excludeId,
        CancellationToken cancellationToken = default)
    {
        // "Active" = mọi status chưa final: Pending (chờ PM), PendingApprover (đã forward, chờ HR/DM), Approved.
        // Rejected/Cancelled loại trừ vì không còn chiếm khung giờ.
        var query = _context.OvertimeRequests
            .AsNoTracking()
            .Where(r => r.EmployeeInformationId == employeeInformationId
                     && r.WorkDate == workDate
                     && (r.Status == OtRequestStatus.Pending
                         || r.Status == OtRequestStatus.PendingApprover
                         || r.Status == OtRequestStatus.Approved));

        if (excludeId.HasValue)
            query = query.Where(r => r.Id != excludeId.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OvertimeRequest>> GetApprovedWithoutAttendanceOnDateAsync(
        DateOnly workDate, CancellationToken cancellationToken = default)
    {
        // LEFT JOIN AttendanceRecords via OvertimeRequestId, filter WHERE record IS NULL.
        var query =
            from ot in _context.OvertimeRequests.AsNoTracking()
                .Include(o => o.EmployeeInformation)
                .Include(o => o.Project)
            where ot.WorkDate == workDate
               && ot.Status == OtRequestStatus.Approved
            join rec in _context.AttendanceRecords.AsNoTracking()
                on ot.Id equals rec.OvertimeRequestId into recs
            from rec in recs.DefaultIfEmpty()
            where rec == null
            orderby ot.EmployeeInformation.FullName, ot.OtStartTime
            select ot;

        return await query.ToListAsync(cancellationToken);
    }
}