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
    /// <summary>
    /// Floor cho hệ số ban đêm — Điều 98.2 BLLĐ: ≥130% lương ngày bình thường
    /// (= 100% + 30% phụ trội). Composer cộng dồn (WorkRateNight − 1.0) vào
    /// base rate, không multiply.
    /// </summary>
    public const decimal Night = 1.30m;

    public static void EnsureValid(decimal workRateNormal, decimal workRateWeeklyRest,
        decimal workRateHoliday, decimal workRateNight)
    {
        if (workRateNormal < Normal)
            throw new DomainValidationException(
                $"WorkRateNormal ({workRateNormal}) is lower than legal floor ({Normal}).");

        if (workRateWeeklyRest < WeeklyRest)
            throw new DomainValidationException(
                $"WorkRateWeeklyRest ({workRateWeeklyRest}) is lower than legal floor ({WeeklyRest}).");

        if (workRateHoliday < Holiday)
            throw new DomainValidationException(
                $"WorkRateHoliday ({workRateHoliday}) is lower than legal floor ({Holiday}).");

        if (workRateNight < Night)
            throw new DomainValidationException(
                $"WorkRateNight ({workRateNight}) is lower than legal floor ({Night}) — Điều 98.2 BLLĐ.");
    }
}
