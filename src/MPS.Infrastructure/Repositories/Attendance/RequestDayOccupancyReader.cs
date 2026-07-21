using Microsoft.EntityFrameworkCore;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

/// <summary>
/// Đọc chéo 3 bảng đơn (LeaveRequests / WfhRequests / LateEarlyRequests) để trả về ngày đã bị chiếm.
/// Xem <see cref="IRequestDayOccupancyReader"/> cho ngữ nghĩa [D8].
/// </summary>
public sealed class RequestDayOccupancyReader : IRequestDayOccupancyReader
{
    private readonly AppDbContext _context;

    public RequestDayOccupancyReader(AppDbContext context)
    {
        _context = context;
    }

    public async Task<OccupiedDay?> FindFirstOccupiedAsync(
        int employeeInformationId,
        DateOnly start,
        DateOnly end,
        RequestDayKind? excludeKind = null,
        int? excludeRequestId = null,
        CancellationToken cancellationToken = default)
    {
        if (end < start) (start, end) = (end, start);

        var candidates = new List<OccupiedDay>();

        var leave = await FirstOverlappingLeaveAsync(
            employeeInformationId, start, end,
            excludeKind == RequestDayKind.Leave ? excludeRequestId : null,
            cancellationToken);
        if (leave is { } leaveDay) candidates.Add(new OccupiedDay(Clamp(leaveDay, start), RequestDayKind.Leave));

        var wfh = await FirstOverlappingWfhAsync(
            employeeInformationId, start, end,
            excludeKind == RequestDayKind.Wfh ? excludeRequestId : null,
            cancellationToken);
        if (wfh is { } wfhDay) candidates.Add(new OccupiedDay(Clamp(wfhDay, start), RequestDayKind.Wfh));

        var lateEarly = await FirstOverlappingLateEarlyAsync(
            employeeInformationId, start, end,
            excludeKind == RequestDayKind.LateEarly ? excludeRequestId : null,
            cancellationToken);
        if (lateEarly is { } leDay) candidates.Add(new OccupiedDay(leDay, RequestDayKind.LateEarly));

        return candidates.OrderBy(c => c.Date).FirstOrDefault();
    }

    public async Task<DateOnly?> FindFirstApprovedLeaveDayAsync(
        int employeeInformationId,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default)
    {
        if (end < start) (start, end) = (end, start);

        var found = await _context.LeaveRequests
            .AsNoTracking()
            .Where(r => r.EmployeeInformationId == employeeInformationId
                && r.Status == LeaveRequestStatus.Approved
                && r.StartDate <= end
                && r.EndDate >= start)
            .OrderBy(r => r.StartDate)
            .Select(r => (DateOnly?)r.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        return found is { } day ? Clamp(day, start) : null;
    }

    private Task<DateOnly?> FirstOverlappingLeaveAsync(
        int employeeInformationId, DateOnly start, DateOnly end, int? excludeId, CancellationToken ct)
    {
        var query = _context.LeaveRequests
            .AsNoTracking()
            .Where(r => r.EmployeeInformationId == employeeInformationId
                && (r.Status == LeaveRequestStatus.Pending || r.Status == LeaveRequestStatus.Approved)
                && r.StartDate <= end
                && r.EndDate >= start);

        if (excludeId.HasValue) query = query.Where(r => r.Id != excludeId.Value);

        return query.OrderBy(r => r.StartDate).Select(r => (DateOnly?)r.StartDate).FirstOrDefaultAsync(ct);
    }

    private Task<DateOnly?> FirstOverlappingWfhAsync(
        int employeeInformationId, DateOnly start, DateOnly end, int? excludeId, CancellationToken ct)
    {
        var query = _context.WfhRequests
            .AsNoTracking()
            .Where(r => r.EmployeeInformationId == employeeInformationId
                && (r.Status == WfhRequestStatus.Pending || r.Status == WfhRequestStatus.Approved)
                && r.StartDate <= end
                && r.EndDate >= start);

        if (excludeId.HasValue) query = query.Where(r => r.Id != excludeId.Value);

        return query.OrderBy(r => r.StartDate).Select(r => (DateOnly?)r.StartDate).FirstOrDefaultAsync(ct);
    }

    private Task<DateOnly?> FirstOverlappingLateEarlyAsync(
        int employeeInformationId, DateOnly start, DateOnly end, int? excludeId, CancellationToken ct)
    {
        var query = _context.LateEarlyRequests
            .AsNoTracking()
            .Where(r => r.EmployeeInformationId == employeeInformationId
                && (r.Status == LateEarlyRequestStatus.Pending || r.Status == LateEarlyRequestStatus.Approved)
                && r.WorkDate >= start
                && r.WorkDate <= end);

        if (excludeId.HasValue) query = query.Where(r => r.Id != excludeId.Value);

        return query.OrderBy(r => r.WorkDate).Select(r => (DateOnly?)r.WorkDate).FirstOrDefaultAsync(ct);
    }

    // Đơn có thể bắt đầu TRƯỚC khoảng đang xét — ngày va chạm thực tế là ngày đầu của khoảng.
    private static DateOnly Clamp(DateOnly candidate, DateOnly start) => candidate < start ? start : candidate;
}
