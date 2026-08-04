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

    /// <summary>Ngày mặc định cho các ca không quan tâm tới mốc đổi luật.</summary>
    private static readonly DateOnly AnyDay = new(2026, 6, 10);

    private static IReadOnlyList<OvertimePayCalculator.OvertimeSegment> Build(
        TimeOnly start, TimeOnly end, DayType d, DayType next) =>
        OvertimeSegmentBuilder.Build(AnyDay, start, end, d, next, LaborLawTimeline.Uniform(Labor()));

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
        var (full, _) = OvertimePayCalculator.Accumulate(
            segs, 50_000m, Multiplier(), LaborLawTimeline.Uniform(Labor()));

        full.Should().Be(590_000m);
    }

    // ===== [Mục 1] Khung giờ ĐÊM đổi giữa kỳ — mỗi ngày lịch tách theo khung của chính nó =====

    [Fact]
    public void KhungGioDemDoiGiuaKy_NgayTruocMoc_TachTheoKhungCu()
    {
        // 21:00→23:00 ngày 14/06. Khung cũ đêm bắt đầu 22:00 ⇒ 1h ngày + 1h đêm.
        var segs = OvertimeSegmentBuilder.Build(
            new DateOnly(2026, 6, 14), new TimeOnly(21, 0), new TimeOnly(23, 0),
            DayType.Normal, DayType.Normal, SplitJune());

        segs.Should().HaveCount(1);
        segs[0].DayHours.Should().Be(1m);
        segs[0].NightHours.Should().Be(1m);
        segs[0].Date.Should().Be(new DateOnly(2026, 6, 14));
    }

    [Fact]
    public void KhungGioDemDoiGiuaKy_TuMocTroDi_TachTheoKhungMoi()
    {
        // Cùng 21:00→23:00 nhưng ngày 15/06. Khung mới đêm bắt đầu 21:00 ⇒ 0h ngày + 2h đêm.
        var segs = OvertimeSegmentBuilder.Build(
            new DateOnly(2026, 6, 15), new TimeOnly(21, 0), new TimeOnly(23, 0),
            DayType.Normal, DayType.Normal, SplitJune());

        segs.Should().HaveCount(1);
        segs[0].DayHours.Should().Be(0m);
        segs[0].NightHours.Should().Be(2m);
    }

    [Fact]
    public void VatNuaDemQuaMocDoiLuat_MoiNuaDungKhungCuaNgayMinh()
    {
        // OT 23:00 ngày 14/06 → 07:00 ngày 15/06, đúng lúc luật đổi khung đêm 22–06 ⇒ 21–05.
        //   Phần D  (23:00–24:00): khung CŨ 22–06 ⇒ 1h đêm.
        //   Phần D+1 (00:00–07:00): khung MỚI 21–05 ⇒ 5h đêm (00–05) + 2h ngày (05–07).
        var segs = OvertimeSegmentBuilder.Build(
            new DateOnly(2026, 6, 14), new TimeOnly(23, 0), new TimeOnly(7, 0),
            DayType.Normal, DayType.Normal, SplitJune());

        segs.Should().HaveCount(2);
        segs[0].Date.Should().Be(new DateOnly(2026, 6, 14));
        segs[0].NightHours.Should().Be(1m);
        segs[0].DayHours.Should().Be(0m);

        segs[1].Date.Should().Be(new DateOnly(2026, 6, 15));
        segs[1].NightHours.Should().Be(5m);
        segs[1].DayHours.Should().Be(2m);
    }

    private static IOvertimeMultiplierCalculator Multiplier() =>
        new OvertimeMultiplierCalculator(Mock.Of<IComplianceLookupService>());

    private static LaborLawConstantsSnapshot Labor(
        int versionId = 1, int nightStartHour = 22, int nightEndHour = 6) => new(
        VersionId: versionId, ValidFrom: new DateOnly(2021, 1, 1),
        ProbationSalaryMinPercent: 85m,
        OvertimeNightExtraPercent: 20m, NightWorkSurchargePercent: 30m, OvertimeNormalUpliftPercent: 50m,
        NightStartTime: new TimeOnly(nightStartHour, 0), NightEndTime: new TimeOnly(nightEndHour, 0),
        StandardWorkingHoursPerDay: 8m, MaxRegularHoursPerWeek: 48m, StandardWorkingDaysPerMonth: 26m,
        AnnualLeaveMinDays: 12,
        WorkRateNormalFloor: 100m, WorkRateWeeklyRestFloor: 200m, WorkRateHolidayFloor: 300m,
        OvertimeMaxHoursPerDay: 4m, OvertimeMaxHoursPerMonth: 40m, OvertimeMaxHoursPerYear: 300,
        LegalReference: "BLLĐ 2019 Điều 98");

    /// <summary>Kỳ 06/2026: tới 14/06 khung đêm 22:00–06:00, từ 15/06 đổi thành 21:00–05:00.</summary>
    private static LaborLawTimeline SplitJune()
    {
        var oldLaw = Labor(versionId: 1);
        var newLaw = Labor(versionId: 2, nightStartHour: 21, nightEndHour: 5);
        var byDate = new Dictionary<DateOnly, LaborLawConstantsSnapshot>();
        for (var d = new DateOnly(2026, 6, 1); d <= new DateOnly(2026, 7, 1); d = d.AddDays(1))
            byDate[d] = d.Month == 7 || d.Day >= 15 ? newLaw : oldLaw;
        return new LaborLawTimeline(byDate, newLaw);
    }
}
