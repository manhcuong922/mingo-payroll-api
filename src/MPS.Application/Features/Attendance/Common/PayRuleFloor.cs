using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Attendance.Common;

/// <summary>
/// BR-22 - Work-rate floors from Labor Code Article 98.
/// Company policy may set rates higher than the floor, never lower.
/// </summary>
public static class PayRuleFloor
{
    public const decimal Normal = 1.00m;
    public const decimal WeeklyRest = 2.00m;
    public const decimal Holiday = 3.00m;

    public static void EnsureValid(decimal workRateNormal, decimal workRateWeeklyRest, decimal workRateHoliday)
    {
        if (workRateNormal < Normal)
            throw new DomainValidationException(
                $"BR-22: WorkRateNormal ({workRateNormal}) is lower than legal floor ({Normal}).");

        if (workRateWeeklyRest < WeeklyRest)
            throw new DomainValidationException(
                $"BR-22: WorkRateWeeklyRest ({workRateWeeklyRest}) is lower than legal floor ({WeeklyRest}).");

        if (workRateHoliday < Holiday)
            throw new DomainValidationException(
                $"BR-22: WorkRateHoliday ({workRateHoliday}) is lower than legal floor ({Holiday}).");
    }
}
