using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Common.Interfaces;

public interface IAttendanceEditLockChecker
{
    Task<AttendanceLockStatusDto> GetStatusAsync(
        DateOnly workDate,
        CancellationToken cancellationToken = default);

    Task EnsureCanModifyAsync(
        DateOnly workDate,
        string? overrideReason,
        CancellationToken cancellationToken = default);
}
