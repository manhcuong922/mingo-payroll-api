using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Tests.Features.Attendance;

public sealed class OvertimeMultiplierCalculatorTests
{
    // Snapshot mặc định theo BLLĐ 2019 + TT 18/2021 — khớp seeder.
    private static LaborLawConstantsSnapshot DefaultSnapshot() => new(
        VersionId: 1,
        ValidFrom: new DateOnly(2021, 1, 1),
        ProbationSalaryMinPercent: 85m,
        OvertimeNightExtraPercent: 20m,
        NightWorkSurchargePercent: 30m,
        OvertimeNormalUpliftPercent: 50m,
        NightStartTime: new TimeOnly(22, 0),
        NightEndTime: new TimeOnly(6, 0),
        StandardWorkingHoursPerDay: 8m,
        MaxRegularHoursPerWeek: 48m,
        StandardWorkingDaysPerMonth: 26m,
        AnnualLeaveMinDays: 12,
        WorkRateNormalFloor: 100m,
        WorkRateWeeklyRestFloor: 200m,
        WorkRateHolidayFloor: 300m,
        OvertimeMaxHoursPerDay: 4m,
        OvertimeMaxHoursPerMonth: 40m,
        OvertimeMaxHoursPerYear: 300,
        LegalReference: "BLLĐ 2019 Điều 98");

    private static OvertimeMultiplierCalculator NewCalculator() =>
        new(Mock.Of<IComplianceLookupService>());

    [Theory]
    // ── Day OT ──
    [InlineData(DayType.Normal,        false, false, 150)] // 100 + 50
    [InlineData(DayType.Normal,        false, true,  150)] // hasPrevDayOt không áp cho day OT
    [InlineData(DayType.WeeklyRestDay, false, false, 200)]
    [InlineData(DayType.PublicHoliday, false, false, 300)]
    [InlineData(DayType.PaidLeaveDay,  false, false, 300)] // Đ.98.1.c gộp lễ + có lương
    // ── Night OT ──
    [InlineData(DayType.Normal,        true,  false, 200)] // 150 + 30 + 20%*100
    [InlineData(DayType.Normal,        true,  true,  210)] // 150 + 30 + 20%*150
    [InlineData(DayType.WeeklyRestDay, true,  false, 270)] // 200 + 30 + 20%*200
    [InlineData(DayType.PublicHoliday, true,  false, 390)] // 300 + 30 + 20%*300
    [InlineData(DayType.PaidLeaveDay,  true,  false, 390)]
    public void Calculate_Matches_LegalFloors(DayType dayType, bool isNight, bool hasPrevDayOt, int expected)
    {
        var result = NewCalculator().Calculate(dayType, isNight, hasPrevDayOt, DefaultSnapshot());

        result.Should().Be(expected);
    }

    [Fact]
    public void Calculate_UsesConfiguredFloor_NotHardcoded()
    {
        // Công ty ưu đãi: WorkRateNormalFloor = 120 (thay vì 100) → OT ngày = 120 + 50 = 170.
        var snapshot = DefaultSnapshot() with { WorkRateNormalFloor = 120m };

        var result = NewCalculator().Calculate(DayType.Normal, isNight: false, hasPrevDayOt: false, snapshot);

        result.Should().Be(170m);
    }

    [Fact]
    public void Calculate_UsesConfiguredNightExtra_NotHardcoded()
    {
        // Ngành đặc thù: OvertimeNightExtraPercent = 25 (thay vì 20) → Night Normal noPrev = 150 + 30 + 25%*100 = 205.
        var snapshot = DefaultSnapshot() with { OvertimeNightExtraPercent = 25m };

        var result = NewCalculator().Calculate(DayType.Normal, isNight: true, hasPrevDayOt: false, snapshot);

        result.Should().Be(205m);
    }

    [Fact]
    public async Task CalculateAtAsync_LoadsSnapshotAndDelegates()
    {
        var snapshot = DefaultSnapshot();
        var workDate = new DateOnly(2026, 7, 10);
        var mock = new Mock<IComplianceLookupService>();
        mock.Setup(s => s.GetLaborLawConstantsAtAsync(workDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var calc = new OvertimeMultiplierCalculator(mock.Object);
        var result = await calc.CalculateAtAsync(DayType.WeeklyRestDay, isNight: true, hasPrevDayOt: false, workDate, CancellationToken.None);

        result.Should().Be(270m);
        mock.Verify(s => s.GetLaborLawConstantsAtAsync(workDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CalculateAtAsync_Throws_When_NoActiveLaborLaw()
    {
        var mock = new Mock<IComplianceLookupService>();
        mock.Setup(s => s.GetLaborLawConstantsAtAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No LaborLawConstantsVersion active at 2026-07-10."));

        var calc = new OvertimeMultiplierCalculator(mock.Object);
        var act = () => calc.CalculateAtAsync(DayType.Normal, false, false, new DateOnly(2026, 7, 10), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
