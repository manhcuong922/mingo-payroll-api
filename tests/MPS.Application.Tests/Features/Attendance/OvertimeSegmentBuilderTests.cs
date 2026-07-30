using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Enums.Attendance;
using Xunit;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// Case 3 + 5 — dựng segment OT từ khung giờ thô: tách theo ngày lịch khi vắt nửa đêm + đặt cờ
/// "đêm sau ngày OT" (Đ.98.3). Khung đêm chuẩn 22:00–06:00.
/// </summary>
public sealed class OvertimeSegmentBuilderTests
{
    private static readonly TimeOnly NightStart = new(22, 0);
    private static readonly TimeOnly NightEnd = new(6, 0);

    private static IReadOnlyList<OvertimePayCalculator.OvertimeSegment> Build(
        TimeOnly start, TimeOnly end, DayType d, DayType next) =>
        OvertimeSegmentBuilder.Build(start, end, d, next, NightStart, NightEnd);

    [Fact]
    public void DayThenNight_SameDay_SetsHasPrevDayOt()
    {
        // 20:00→23:00: 2h ngày (20–22) + 1h đêm (22–23), đêm ĐỨNG SAU ngày ⇒ HasPrevDayOt.
        var segs = Build(new TimeOnly(20, 0), new TimeOnly(23, 0), DayType.Normal, DayType.Normal);

        segs.Should().HaveCount(1);
        segs[0].DayType.Should().Be(DayType.Normal);
        segs[0].DayHours.Should().Be(2m);
        segs[0].NightHours.Should().Be(1m);
        segs[0].HasPrevDayOt.Should().BeTrue();
    }

    [Fact]
    public void PureNight_NoDayBefore_NoHasPrevDayOt()
    {
        // 22:00→02:00 vắt nửa đêm, cả 2 ngày thường: 2 segment đêm, KHÔNG có giờ ngày đứng trước.
        var segs = Build(new TimeOnly(22, 0), new TimeOnly(2, 0), DayType.Normal, DayType.Normal);

        segs.Should().HaveCount(2);
        segs.Should().OnlyContain(s => s.HasPrevDayOt == false);
        segs[0].NightHours.Should().Be(2m); // 22:00–24:00
        segs[1].NightHours.Should().Be(2m); // 00:00–02:00
        segs.Sum(s => s.DayHours).Should().Be(0m);
    }

    [Fact]
    public void CrossMidnight_IntoHoliday_SplitsDayTypePerCalendarDay()
    {
        // Case 5: OT 22:00→02:00 ngày thường, D+1 là NGÀY LỄ.
        var segs = Build(new TimeOnly(22, 0), new TimeOnly(2, 0), DayType.Normal, DayType.PublicHoliday);

        segs.Should().HaveCount(2);
        // Phần trên ngày D (22:00–24:00) = đêm ngày thường.
        segs[0].DayType.Should().Be(DayType.Normal);
        segs[0].NightHours.Should().Be(2m);
        // Phần sau nửa đêm (00:00–02:00) = đêm NGÀY LỄ.
        segs[1].DayType.Should().Be(DayType.PublicHoliday);
        segs[1].NightHours.Should().Be(2m);
    }

    [Fact]
    public void NightThenDay_EarlyMorning_NoHasPrevDayOt()
    {
        // 04:00→08:00: đêm (04–06) rồi ngày (06–08). Đêm ĐỨNG TRƯỚC ngày ⇒ KHÔNG HasPrevDayOt.
        var segs = Build(new TimeOnly(4, 0), new TimeOnly(8, 0), DayType.Normal, DayType.Normal);

        segs.Should().HaveCount(1);
        segs[0].NightHours.Should().Be(2m);
        segs[0].DayHours.Should().Be(2m);
        segs[0].HasPrevDayOt.Should().BeFalse();
    }

    [Fact]
    public void EndsExactlyMidnight_DropsEmptyNextDaySegment()
    {
        // 20:00→24:00 (end=00:00): chỉ 1 segment trên ngày D, phần D+1 rỗng bị bỏ.
        var segs = Build(new TimeOnly(20, 0), new TimeOnly(0, 0), DayType.Normal, DayType.PublicHoliday);

        segs.Should().HaveCount(1);
        segs[0].DayType.Should().Be(DayType.Normal);
        segs[0].DayHours.Should().Be(2m);
        segs[0].NightHours.Should().Be(2m);
        segs[0].HasPrevDayOt.Should().BeTrue();
    }

    [Fact]
    public void EndToEnd_CrossMidnightIntoHoliday_PayMatchesLegalFloors()
    {
        // Kịch bản người dùng (case 5): 22:00→02:00, ngày thường → ngày lễ. hourlyRate = 50k.
        //   22:00–24:00 đêm ngày thường (không prevDayOt) = 200% ⇒ 50k×2×2.0 = 200k
        //   00:00–02:00 đêm NGÀY LỄ                       = 390% ⇒ 50k×2×3.9 = 390k
        var segs = Build(new TimeOnly(22, 0), new TimeOnly(2, 0), DayType.Normal, DayType.PublicHoliday);
        var (full, _) = OvertimePayCalculator.Accumulate(segs, 50_000m, Multiplier(), Labor());

        full.Should().Be(590_000m);
    }

    private static IOvertimeMultiplierCalculator Multiplier() =>
        new OvertimeMultiplierCalculator(Mock.Of<IComplianceLookupService>());

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
}
