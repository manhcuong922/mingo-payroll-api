using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class EmployeeShiftAssignmentRepository : IEmployeeShiftAssignmentRepository
{
    private readonly AppDbContext _context;

    public EmployeeShiftAssignmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<EmployeeShiftAssignment?> GetByKeyAsync(
        int employeeInformationId,
        DateOnly workDate,
        int shiftTemplateId,
        CancellationToken cancellationToken = default) =>
        _context.EmployeeShiftAssignments
            .AsNoTracking()
            .Include(a => a.ShiftTemplate)
            .FirstOrDefaultAsync(
                a => a.EmployeeInformationId == employeeInformationId
                  && a.WorkDate == workDate
                  && a.ShiftTemplateId == shiftTemplateId,
                cancellationToken);

    public async Task<IReadOnlyList<EmployeeShiftAssignment>> GetByEmployeeAndDateAsync(
        int employeeInformationId,
        DateOnly workDate,
        CancellationToken cancellationToken = default) =>
        await _context.EmployeeShiftAssignments
            .AsNoTracking()
            .Include(a => a.ShiftTemplate)
            .Where(a => a.EmployeeInformationId == employeeInformationId && a.WorkDate == workDate)
            .OrderBy(a => a.ShiftTemplate.StartTime)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<EmployeeShiftAssignment>> GetByDateAsync(DateOnly workDate, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeShiftAssignments
            .AsNoTracking()
            .Include(a => a.ShiftTemplate)
            .Include(a => a.EmployeeInformation)
            .Where(a => a.WorkDate == workDate)
            .OrderBy(a => a.EmployeeInformation.FullName)
            .ThenBy(a => a.ShiftTemplate.StartTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeShiftAssignment>> GetByEmployeesAndDateRangeAsync(
        IReadOnlyCollection<int> employeeInformationIds,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (employeeInformationIds.Count == 0)
        {
            return Array.Empty<EmployeeShiftAssignment>();
        }

        return await _context.EmployeeShiftAssignments
            .AsNoTracking()
            .Include(a => a.ShiftTemplate)
            .Include(a => a.EmployeeInformation)
            .Where(a => employeeInformationIds.Contains(a.EmployeeInformationId)
                && a.WorkDate >= fromDate
                && a.WorkDate <= toDate)
            .OrderBy(a => a.EmployeeInformationId)
            .ThenBy(a => a.WorkDate)
            .ThenBy(a => a.ShiftTemplate.StartTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(int EmployeeInformationId, DateOnly WorkDate, int ShiftTemplateId)>> GetLockedShiftsAsync(
        IReadOnlyCollection<int> employeeInformationIds,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (employeeInformationIds.Count == 0)
        {
            return Array.Empty<(int, DateOnly, int)>();
        }

        var rows = await _context.AttendanceRecords
            .AsNoTracking()
            .Where(r => employeeInformationIds.Contains(r.EmployeeInformationId)
                && r.WorkDate >= fromDate
                && r.WorkDate <= toDate)
            .Select(r => new { r.EmployeeInformationId, r.WorkDate, r.ShiftTemplateId })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.EmployeeInformationId, r.WorkDate, r.ShiftTemplateId)).ToList();
    }

    public async Task ApplyBatchAsync(
        IReadOnlyCollection<EmployeeShiftAssignment> toAdd,
        IReadOnlyCollection<(int Id, int NewShiftTemplateId, int? NewScheduleTemplateId)> toUpdate,
        IReadOnlyCollection<int> toRemoveIds,
        CancellationToken cancellationToken = default)
    {
        if (toAdd.Count == 0 && toUpdate.Count == 0 && toRemoveIds.Count == 0)
        {
            return;
        }

        if (toAdd.Count > 0)
        {
            await _context.EmployeeShiftAssignments.AddRangeAsync(toAdd, cancellationToken);
        }

        if (toUpdate.Count > 0)
        {
            var updateIds = toUpdate.Select(u => u.Id).ToHashSet();
            var tracked = await _context.EmployeeShiftAssignments
                .Where(a => updateIds.Contains(a.Id))
                .ToListAsync(cancellationToken);

            var patchById = toUpdate.ToDictionary(u => u.Id);
            foreach (var entity in tracked)
            {
                if (patchById.TryGetValue(entity.Id, out var patch))
                {
                    entity.ShiftTemplateId = patch.NewShiftTemplateId;
                    if (patch.NewScheduleTemplateId.HasValue)
                    {
                        entity.ScheduleTemplateId = patch.NewScheduleTemplateId;
                    }
                }
            }
        }

        if (toRemoveIds.Count > 0)
        {
            var idSet = toRemoveIds.ToHashSet();
            var tracked = await _context.EmployeeShiftAssignments
                .Where(a => idSet.Contains(a.Id))
                .ToListAsync(cancellationToken);
            _context.EmployeeShiftAssignments.RemoveRange(tracked);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(int EmployeeInformationId, DateOnly WorkDate, int OtherScheduleTemplateId, string OtherScheduleTemplateName, string EmployeeFullName)>>
        GetOwnedConflictsAsync(
            IReadOnlyCollection<(int EmployeeInformationId, DateOnly WorkDate)> pairs,
            int? excludeScheduleTemplateId,
            TimeOnly newShiftStart,
            TimeOnly newShiftEnd,
            CancellationToken cancellationToken = default)
    {
        if (pairs.Count == 0)
        {
            return Array.Empty<(int, DateOnly, int, string, string)>();
        }

        var employeeIds = pairs.Select(p => p.EmployeeInformationId).Distinct().ToList();
        var dates = pairs.Select(p => p.WorkDate).Distinct().ToList();
        var pairSet = pairs.ToHashSet();
        var minDate = dates.Min();
        var maxDate = dates.Max();
        var queryFromDate = minDate.AddDays(-1);
        var queryToDate = maxDate.AddDays(1);

        // (1) Assignments được sở hữu bởi template khác — include ShiftTemplate để compare giờ.
        var ownedRows = await _context.EmployeeShiftAssignments
            .AsNoTracking()
            .Where(a => a.ScheduleTemplateId != null
                && (excludeScheduleTemplateId == null || a.ScheduleTemplateId != excludeScheduleTemplateId)
                && employeeIds.Contains(a.EmployeeInformationId)
                && a.WorkDate >= queryFromDate
                && a.WorkDate <= queryToDate)
            .Select(a => new
            {
                a.EmployeeInformationId,
                a.WorkDate,
                OwnerId = a.ScheduleTemplateId!.Value,
                OwnerName = a.ScheduleTemplate!.Name,
                EmployeeFullName = a.EmployeeInformation.FullName,
                ShiftStart = a.ShiftTemplate.StartTime,
                ShiftEnd = a.ShiftTemplate.EndTime
            })
            .ToListAsync(cancellationToken);

        // (2) Orphan assignments (ScheduleTemplateId = NULL).
        var orphanRows = await _context.EmployeeShiftAssignments
            .AsNoTracking()
            .Where(a => a.ScheduleTemplateId == null
                && employeeIds.Contains(a.EmployeeInformationId)
                && a.WorkDate >= queryFromDate
                && a.WorkDate <= queryToDate)
            .Select(a => new
            {
                a.EmployeeInformationId,
                a.WorkDate,
                EmployeeFullName = a.EmployeeInformation.FullName,
                ShiftStart = a.ShiftTemplate.StartTime,
                ShiftEnd = a.ShiftTemplate.EndTime
            })
            .ToListAsync(cancellationToken);

        const int orphanSentinelId = -1;
        const string orphanSentinelName = "(ca nhân viên đã được phân công)";

        var result = new List<(int, DateOnly, int, string, string)>();

        result.AddRange(
            ownedRows
                .Where(r => pairs.Any(p => p.EmployeeInformationId == r.EmployeeInformationId
                         && Math.Abs(p.WorkDate.DayNumber - r.WorkDate.DayNumber) <= 1
                         && pairSet.Contains((p.EmployeeInformationId, p.WorkDate))
                         && TimeRangesOverlap(p.WorkDate, newShiftStart, newShiftEnd, r.WorkDate, r.ShiftStart, r.ShiftEnd)))
                .Select(r => (r.EmployeeInformationId, r.WorkDate, r.OwnerId, r.OwnerName, r.EmployeeFullName)));

        result.AddRange(
            orphanRows
                .Where(r => pairs.Any(p => p.EmployeeInformationId == r.EmployeeInformationId
                         && Math.Abs(p.WorkDate.DayNumber - r.WorkDate.DayNumber) <= 1
                         && pairSet.Contains((p.EmployeeInformationId, p.WorkDate))
                         && TimeRangesOverlap(p.WorkDate, newShiftStart, newShiftEnd, r.WorkDate, r.ShiftStart, r.ShiftEnd)))
                .Select(r => (r.EmployeeInformationId, r.WorkDate, orphanSentinelId, orphanSentinelName, r.EmployeeFullName)));

        return result;
    }

    /// <summary>
    /// Absolute overlap theo WorkDate; End &lt; Start được hiểu là kết thúc vào ngày hôm sau.
    /// Half-open interval [Start, End): hai ca touch tại cùng thời điểm KHÔNG overlap.
    /// </summary>
    private static bool TimeRangesOverlap(
        DateOnly aDate,
        TimeOnly aStart,
        TimeOnly aEnd,
        DateOnly bDate,
        TimeOnly bStart,
        TimeOnly bEnd)
    {
        var (aAbsoluteStart, aAbsoluteEnd) = ToAbsoluteRange(aDate, aStart, aEnd);
        var (bAbsoluteStart, bAbsoluteEnd) = ToAbsoluteRange(bDate, bStart, bEnd);
        return aAbsoluteStart < bAbsoluteEnd && bAbsoluteStart < aAbsoluteEnd;
    }

    private static (long Start, long End) ToAbsoluteRange(DateOnly date, TimeOnly start, TimeOnly end)
    {
        const int minutesPerDay = 24 * 60;
        var baseMinute = (long)date.DayNumber * minutesPerDay;
        var s = baseMinute + start.Hour * 60 + start.Minute;
        var e = baseMinute + end.Hour * 60 + end.Minute;
        if (e <= s) e += minutesPerDay;
        return (s, e);
    }

    public async Task<int> DeleteStaleByScheduleTemplateAsync(
        int scheduleTemplateId,
        IReadOnlyCollection<DateOnly> retainDates,
        CancellationToken cancellationToken = default)
    {
        var owned = await _context.EmployeeShiftAssignments
            .Where(a => a.ScheduleTemplateId == scheduleTemplateId)
            .ToListAsync(cancellationToken);
        if (owned.Count == 0) return 0;

        var retainSet = retainDates.ToHashSet();

        var stale = owned.Where(a => !retainSet.Contains(a.WorkDate)).ToList();
        if (stale.Count == 0) return 0;

        var staleEmployeeIds = stale.Select(a => a.EmployeeInformationId).Distinct().ToList();
        var minDate = stale.Min(a => a.WorkDate);
        var maxDate = stale.Max(a => a.WorkDate);

        var lockedRows = await _context.AttendanceRecords
            .AsNoTracking()
            .Where(r => staleEmployeeIds.Contains(r.EmployeeInformationId)
                && r.WorkDate >= minDate
                && r.WorkDate <= maxDate)
            .Select(r => new { r.EmployeeInformationId, r.WorkDate, r.ShiftTemplateId })
            .ToListAsync(cancellationToken);
        var lockedSet = lockedRows.Select(r => (r.EmployeeInformationId, r.WorkDate, r.ShiftTemplateId)).ToHashSet();

        var toRemove = stale
            .Where(a => !lockedSet.Contains((a.EmployeeInformationId, a.WorkDate, a.ShiftTemplateId)))
            .ToList();
        if (toRemove.Count == 0) return 0;

        _context.EmployeeShiftAssignments.RemoveRange(toRemove);
        await _context.SaveChangesAsync(cancellationToken);
        return toRemove.Count;
    }
}
