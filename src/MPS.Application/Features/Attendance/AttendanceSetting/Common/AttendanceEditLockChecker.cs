using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceSetting.Common;

public sealed class AttendanceEditLockChecker : IAttendanceEditLockChecker
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly IAttendanceSettingRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public AttendanceEditLockChecker(
        IAttendanceSettingRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<AttendanceLockStatusDto> GetStatusAsync(
        DateOnly workDate,
        CancellationToken cancellationToken = default)
    {
        var setting = await _repository.GetOrCreateDefaultAsync(_clock.UtcNow, cancellationToken);
        var canOverride = _currentUser.HasPermission(Permissions.AttendanceAdjustmentOverrideAll)
            && setting.AllowAdminOverride;

        if (!setting.IsEditLockEnabled)
        {
            return new AttendanceLockStatusDto(
                workDate,
                false,
                false,
                null,
                null,
                canOverride,
                false,
                null);
        }

        var lockedAtLocal = workDate
            .AddDays(setting.LockAfterDays)
            .ToDateTime(setting.DailyEditCutoffTime);
        var lockedAtUtc = lockedAtLocal - VietnamOffset;
        var nowLocal = _clock.UtcNow + VietnamOffset;
        var isLocked = nowLocal >= lockedAtLocal;
        var requiresReason = isLocked && setting.RequireOverrideReason;
        var message = isLocked
            ? $"Dữ liệu chấm công ngày {workDate:dd/MM/yyyy} đã bị khóa từ {lockedAtLocal:HH:mm dd/MM/yyyy}."
            : null;

        return new AttendanceLockStatusDto(
            workDate,
            true,
            isLocked,
            lockedAtUtc,
            lockedAtLocal,
            canOverride,
            requiresReason,
            message);
    }

    public async Task EnsureCanModifyAsync(
        DateOnly workDate,
        string? overrideReason,
        CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(workDate, cancellationToken);
        if (!status.IsLocked) return;

        if (!status.CanOverride)
        {
            throw new DomainValidationException(
                status.Message ?? $"Dữ liệu chấm công ngày {workDate:dd/MM/yyyy} đã bị khóa.");
        }

        if (status.RequiresOverrideReason && string.IsNullOrWhiteSpace(overrideReason))
        {
            throw new DomainValidationException(
                "Cần nhập lý do override khi sửa dữ liệu chấm công đã khóa.");
        }
    }

}
