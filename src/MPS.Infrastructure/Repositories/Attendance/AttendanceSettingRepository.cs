using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class AttendanceSettingRepository : IAttendanceSettingRepository
{
    private readonly AppDbContext _context;

    public AttendanceSettingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AttendanceSetting> GetOrCreateDefaultAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var setting = await _context.AttendanceSettings
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (setting is not null) return setting;

        setting = new AttendanceSetting
        {
            IsEditLockEnabled = false,
            DailyEditCutoffTime = new TimeOnly(18, 0),
            LockAfterDays = 0,
            AllowAdminOverride = true,
            RequireOverrideReason = true,
            CreatedAt = nowUtc,
        };

        await _context.AttendanceSettings.AddAsync(setting, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return setting;
    }

    public async Task<AttendanceSetting> UpdateAsync(
        AttendanceSetting entity,
        CancellationToken cancellationToken = default)
    {
        var tracked = await _context.AttendanceSettings
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (tracked is null)
        {
            await _context.AttendanceSettings.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return entity;
        }

        tracked.IsEditLockEnabled = entity.IsEditLockEnabled;
        tracked.DailyEditCutoffTime = entity.DailyEditCutoffTime;
        tracked.LockAfterDays = entity.LockAfterDays;
        tracked.AllowAdminOverride = entity.AllowAdminOverride;
        tracked.RequireOverrideReason = entity.RequireOverrideReason;
        tracked.LateEarlyMaxOccurrencesPerMonth = entity.LateEarlyMaxOccurrencesPerMonth;
        tracked.LateEarlyMaxMinutesPerOccurrence = entity.LateEarlyMaxMinutesPerOccurrence;
        tracked.LateEarlyBackdateDaysAllowed = entity.LateEarlyBackdateDaysAllowed;
        tracked.WfhMaxDaysPerMonth = entity.WfhMaxDaysPerMonth;
        tracked.WfhMinNoticeDays = entity.WfhMinNoticeDays;
        tracked.WfhMaxDaysPerRequest = entity.WfhMaxDaysPerRequest;
        tracked.WfhWorkPlanMinLength = entity.WfhWorkPlanMinLength;
        tracked.UpdatedAt = entity.UpdatedAt;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return tracked;
    }
}
