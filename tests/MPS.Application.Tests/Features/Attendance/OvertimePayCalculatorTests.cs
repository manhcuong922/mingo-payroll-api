using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// BS-2 — quy giờ OT ra tiền theo hệ số loại-ngày. Dùng OvertimeMultiplierCalculator THẬT (logic hệ số
/// đã có test riêng) để chứng minh phần wiring/số học của PayrollDataCollector đúng, không cần DbContext.
/// </summary>
public sealed class OvertimePayCalculatorTests
{
    private const decimal HourlyRate = 50_000m;

    private static LaborLawConstantsSnapshot Labor() => new(
        VersionId: 1, ValidFrom: new DateOnly(2021, 1, 1),
        ProbationSalaryMinPercent: 85m,
        OvertimeNightExtraPercent: 20m, NightWorkSurchargePercent: 30m, OvertimeNormalUpliftPercent: 50m,
        NightStartTime: new TimeOnly(22, 0), NightEndTime: new TimeOnly(6, 0),
        StandardWorkingHoursPerDay: 8m, MaxRegularHoursPerWeek: 48m, StandardWorkingDaysPerMonth: 26m,
        AnnualLeaveMinDays: 12,
        WorkRateNormalFloor: 100m, WorkRateWeeklyRestFloor: 200m, WorkRateHolidayFloor: 300m,
        OvertimeMaxHoursPerDay: 4m, OvertimeMaxHoursPerMonth: 40m, OvertimeMaxHoursPerYear: 300,
        LegalReference: "BLLĐ 2019 Điều 98");

    private static IOvertimeMultiplierCalculator Multiplier() =>
        new OvertimeMultiplierCalculator(Mock.Of<IComplianceLookupService>());

    private static (decimal Full, decimal Premium) Run(params OvertimePayCalculator.OvertimeSegment[] segs) =>
        OvertimePayCalculator.Accumulate(segs, HourlyRate, Multiplier(), Labor());

    [Fact]
    public void NormalDay_MatchesOldHardcodedRates_NoRegression()
    {
        // Ngày thường: hệ số động PHẢI ra đúng 1.5 (ngày) / 2.0 (đêm) như code cũ.
        var (full, premium) = Run(new OvertimePayCalculator.OvertimeSegment(DayType.Normal, DayHours: 2m, NightHours: 1m));

        // full = 50k×(2×1.5 + 1×2.0) = 50k×5 = 250k ; premium = 50k×(2×0.5 + 1×1.0) = 50k×2 = 100k
        full.Should().Be(250_000m);
        premium.Should().Be(100_000m);
    }

    [Fact]
    public void NormalNight_AfterDayOt_Uses210_Not200()
    {
        // Case 3 (Đ.98.3): đêm ngày thường ĐỨNG SAU giờ ngày OT = 150 + 30 + 20%×150 = 210%.
        var (full, premium) = Run(new OvertimePayCalculator.OvertimeSegment(
            DayType.Normal, DayHours: 2m, NightHours: 1m, HasPrevDayOt: true));

        // full = 50k×(2×1.5 + 1×2.1) = 50k×5.1 = 255k ; premium = 50k×(2×0.5 + 1×1.1) = 50k×2.1 = 105k
        full.Should().Be(255_000m);
        premium.Should().Be(105_000m);
    }

    [Fact]
    public void PublicHoliday_UsesTripleRate_NotHardcoded()
    {
        // BS-2: OT 4 giờ ban ngày vào NGÀY LỄ phải 300%, không phải 150%.
        var (full, premium) = Run(new OvertimePayCalculator.OvertimeSegment(DayType.PublicHoliday, DayHours: 4m, NightHours: 0m));

        // Đúng: 50k×4×3.0 = 600k (cũ sai: 50k×4×1.5 = 300k — trả một nửa).
        full.Should().Be(600_000m);
        premium.Should().Be(400_000m); // 50k×4×(3.0−1) = 400k miễn thuế
    }

    [Fact]
    public void WeeklyRestDay_UsesDoubleRate()
    {
        var (full, _) = Run(new OvertimePayCalculator.OvertimeSegment(DayType.WeeklyRestDay, DayHours: 3m, NightHours: 0m));

        full.Should().Be(300_000m); // 50k×3×2.0
    }

    [Fact]
    public void HolidayNight_UsesConfiguredNightUplift()
    {
        // Đêm ngày lễ: 300 + 30 + 20%×300 = 390%.
        var (full, _) = Run(new OvertimePayCalculator.OvertimeSegment(DayType.PublicHoliday, DayHours: 0m, NightHours: 2m));

        full.Should().Be(390_000m); // 50k×2×3.9
    }

    [Fact]
    public void ZeroHourlyRate_ReturnsZero()
    {
        var result = OvertimePayCalculator.Accumulate(
            new[] { new OvertimePayCalculator.OvertimeSegment(DayType.PublicHoliday, 4m, 0m) },
            hourlyRate: 0m, Multiplier(), Labor());

        result.Should().Be((0m, 0m));
    }
}
