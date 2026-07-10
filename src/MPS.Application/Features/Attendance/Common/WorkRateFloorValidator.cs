using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Attendance.Common;

/// <summary>
/// BR-22 — Validate AttendanceRule work-rate fields against legal floors (Điều 98.1 BLLĐ).
/// Floors are passed in from <c>LaborLawConstantsSnapshot</c>; this helper does not own them.
/// Company policy may set rates higher than the floor, never lower.
/// Unit: percent (VD 100 = 100%, 150 = 150%). Đơn vị đồng bộ giữa AttendanceRule.WorkRate*
/// và LaborLawConstantsVersion.WorkRate*Floor.
/// </summary>
public static class WorkRateFloorValidator
{
    public static void EnsureValid(decimal workRateNormal, decimal workRateWeeklyRest,
        decimal workRateHoliday, decimal normalFloor, decimal weeklyRestFloor, decimal holidayFloor)
    {
        if (workRateNormal < normalFloor)
            throw new DomainValidationException(
                $"WorkRateNormal ({workRateNormal}) is lower than legal floor ({normalFloor}).");

        if (workRateWeeklyRest < weeklyRestFloor)
            throw new DomainValidationException(
                $"WorkRateWeeklyRest ({workRateWeeklyRest}) is lower than legal floor ({weeklyRestFloor}).");

        if (workRateHoliday < holidayFloor)
            throw new DomainValidationException(
                $"WorkRateHoliday ({workRateHoliday}) is lower than legal floor ({holidayFloor}).");
    }
}
