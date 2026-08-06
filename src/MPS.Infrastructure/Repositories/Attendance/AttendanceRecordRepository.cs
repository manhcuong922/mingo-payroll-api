using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class AttendanceRecordRepository : IAttendanceRecordRepository
{
    private readonly AppDbContext _context;

    public AttendanceRecordRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<AttendanceRecord?> GetByKeyAsync(
        int employeeInformationId,
        DateOnly workDate,
        int shiftTemplateId,
        CancellationToken cancellationToken = default) =>
        _context.AttendanceRecords
            .AsNoTracking()
            .Include(r => r.EmployeeInformation)
            .Include(r => r.ShiftTemplate)
            .FirstOrDefaultAsync(
                r => r.EmployeeInformationId == employeeInformationId
                  && r.WorkDate == workDate
                  && r.ShiftTemplateId == shiftTemplateId,
                cancellationToken);

    public async Task<IReadOnlyList<AttendanceRecord>> GetByEmployeeAndDateAsync(
        int employeeInformationId,
        DateOnly workDate,
        CancellationToken cancellationToken = default) =>
        await _context.AttendanceRecords
            .AsNoTracking()
            .Include(r => r.ShiftTemplate)
            .Where(r => r.EmployeeInformationId == employeeInformationId && r.WorkDate == workDate)
            .OrderBy(r => r.ShiftTemplate.StartTime)
            .ToListAsync(cancellationToken);

    public Task<AttendanceRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.AttendanceRecords
            .AsNoTracking()
            .Include(r => r.EmployeeInformation)
            .Include(r => r.ShiftTemplate)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AttendanceRecord>> GetByEmployeeRangeAsync(
        int employeeInformationId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.AttendanceRecords
            .AsNoTracking()
            .Include(r => r.ShiftTemplate)
            .Include(r => r.OvertimeRequest)
                .ThenInclude(o => o!.Project)
            .Where(r => r.EmployeeInformationId == employeeInformationId
                && r.WorkDate >= fromDate
                && r.WorkDate <= toDate)
            .OrderBy(r => r.WorkDate)
            .ThenBy(r => r.ShiftTemplate != null ? r.ShiftTemplate.StartTime : r.OvertimeRequest!.OtStartTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<AttendanceRecord> Items, int Total)> GetSummaryPagedAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int? employeeInformationId,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AttendanceRecords
            .AsNoTracking()
            .Include(r => r.EmployeeInformation)
            .Where(r => r.WorkDate >= fromDate && r.WorkDate <= toDate);

        if (employeeInformationId.HasValue)
            query = query.Where(r => r.EmployeeInformationId == employeeInformationId.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.WorkDate)
            .ThenBy(r => r.EmployeeInformationId)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<AttendanceRecord>>> GetForSummaryByEmployeesAsync(
        IReadOnlyList<int> employeeInformationIds,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (employeeInformationIds.Count == 0)
            return new Dictionary<int, IReadOnlyList<AttendanceRecord>>();

        var records = await _context.AttendanceRecords
            .AsNoTracking()
            .Include(r => r.ShiftTemplate)
                .ThenInclude(s => s.AppliedAttendanceRule)
            .Where(r => employeeInformationIds.Contains(r.EmployeeInformationId)
                     && r.WorkDate >= fromDate
                     && r.WorkDate <= toDate)
            .ToListAsync(cancellationToken);

        return records
            .GroupBy(r => r.EmployeeInformationId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<AttendanceRecord>)g.ToList());
    }

    public Task<AttendanceRecord?> GetByOvertimeRequestIdAsync(
        int overtimeRequestId, CancellationToken cancellationToken = default) =>
        _context.AttendanceRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OvertimeRequestId == overtimeRequestId, cancellationToken);

    public async Task<IReadOnlyList<AttendanceRecord>> GetByDateAsync(DateOnly workDate, CancellationToken cancellationToken = default)
    {
        return await _context.AttendanceRecords
            .AsNoTracking()
            .Include(r => r.EmployeeInformation)
            .Include(r => r.ShiftTemplate)
            .Include(r => r.OvertimeRequest)
                .ThenInclude(o => o!.Project)
            .Where(r => r.WorkDate == workDate)
            .OrderBy(r => r.EmployeeInformation.FullName)
            .ThenBy(r => r.ShiftTemplate != null ? r.ShiftTemplate.StartTime : r.OvertimeRequest!.OtStartTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceRecord>> GetByRangeAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.AttendanceRecords
            .AsNoTracking()
            .Include(r => r.EmployeeInformation)
            .Include(r => r.ShiftTemplate)
            .Include(r => r.OvertimeRequest)
                .ThenInclude(o => o!.Project)
            .Where(r => r.WorkDate >= fromDate && r.WorkDate <= toDate)
            .OrderBy(r => r.WorkDate)
            .ThenBy(r => r.EmployeeInformationId)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> AddAsync(AttendanceRecord entity, CancellationToken cancellationToken = default)
    {
        await _context.AttendanceRecords.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(AttendanceRecord entity, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.AttendanceRecords.FirstOrDefaultAsync(r => r.Id == entity.Id, cancellationToken);
        if (tracked is null) return false;

        tracked.ShiftTemplateId = entity.ShiftTemplateId;
        tracked.LateEarlyRequestId = entity.LateEarlyRequestId;
        tracked.CheckInAt = entity.CheckInAt;
        tracked.CheckOutAt = entity.CheckOutAt;
        tracked.DayHours = entity.DayHours;
        tracked.NightHours = entity.NightHours;
        tracked.Status = entity.Status;
        tracked.Notes = entity.Notes;
        tracked.OverrideReason = entity.OverrideReason;
        tracked.UpdatedAt = entity.UpdatedAt;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
