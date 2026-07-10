using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Common;

public sealed class OvertimeMultiplierCalculator : IOvertimeMultiplierCalculator
{
    private readonly IComplianceLookupService _complianceLookup;

    public OvertimeMultiplierCalculator(IComplianceLookupService complianceLookup)
    {
        _complianceLookup = complianceLookup;
    }

    public decimal Calculate(DayType dayType, bool isNight, bool hasPrevDayOt, LaborLawConstantsSnapshot laborLaw)
    {
        var dayRate = DayOtRate(dayType, laborLaw);
        if (!isNight) return dayRate;

        // OT đêm = OT ngày + phụ cấp làm đêm + 20% × base rate.
        // Base rate cho ngày thường: 100% nếu chưa OT day; 150% (=dayRate) nếu đã OT day.
        // Cho ngày nghỉ tuần/lễ: luôn dùng dayRate (200%/300%).
        var baseHourRate = dayType switch
        {
            DayType.Normal => hasPrevDayOt ? dayRate : laborLaw.WorkRateNormalFloor,
            _ => dayRate,
        };

        return dayRate
            + laborLaw.NightWorkSurchargePercent
            + laborLaw.OvertimeNightExtraPercent * baseHourRate / 100m;
    }

    public async Task<decimal> CalculateAtAsync(
        DayType dayType, bool isNight, bool hasPrevDayOt, DateOnly workDate, CancellationToken cancellationToken)
    {
        var laborLaw = await _complianceLookup.GetLaborLawConstantsAtAsync(workDate, cancellationToken);
        return Calculate(dayType, isNight, hasPrevDayOt, laborLaw);
    }

    // Điều 98.1: ngày thường + uplift 50%; ngày nghỉ tuần = WeeklyRestFloor; ngày lễ / có lương = HolidayFloor.
    private static decimal DayOtRate(DayType dayType, LaborLawConstantsSnapshot laborLaw) => dayType switch
    {
        DayType.Normal        => laborLaw.WorkRateNormalFloor + laborLaw.OvertimeNormalUpliftPercent,
        DayType.WeeklyRestDay => laborLaw.WorkRateWeeklyRestFloor,
        DayType.PublicHoliday => laborLaw.WorkRateHolidayFloor,
        DayType.PaidLeaveDay  => laborLaw.WorkRateHolidayFloor,
        _ => throw new ArgumentOutOfRangeException(nameof(dayType), dayType, "Unsupported DayType for OT multiplier."),
    };
}
