using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Application.Common.Interfaces;

/// <summary>
/// BR-23 — Resolve DayType cho 1 WorkDate theo thứ tự ưu tiên:
/// (1) PublicHoliday → (2) PaidLeaveDay → (3) WeeklyRestDay (mặc định CN) → (4) Normal.
/// </summary>
public interface IDayTypeResolver
{
    Task<AttendanceEnums.DayType> ResolveAsync(
        int employeeInformationId,
        DateOnly workDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<DateOnly, AttendanceEnums.DayType>> ResolveRangeAsync(
        int employeeInformationId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);
}
