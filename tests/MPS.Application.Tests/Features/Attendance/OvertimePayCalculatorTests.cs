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

    /// <summary>Ngày mặc định cho các ca không quan tâm tới mốc đổi luật.</summary>
    private static readonly DateOnly AnyDay = new(2026, 6, 10);

    private static LaborLawConstantsSnapshot Labor(
        int versionId = 1, decimal holiday = 300m, decimal nightSurcharge = 30m, int validFromDay = 1) => new(
        VersionId: versionId, ValidFrom: new DateOnly(2021, 1, validFromDay),
        ProbationSalaryMinPercent: 85m,
        OvertimeNightExtraPercent: 20m, NightWorkSurchargePercent: nightSurcharge, OvertimeNormalUpliftPercent: 50m,
        NightStartTime: new TimeOnly(22, 0), NightEndTime: new TimeOnly(6, 0),
        StandardWorkingHoursPerDay: 8m, MaxRegularHoursPerWeek: 48m, StandardWorkingDaysPerMonth: 26m,
        AnnualLeaveMinDays: 12,
        WorkRateNormalFloor: 100m, WorkRateWeeklyRestFloor: 200m, WorkRateHolidayFloor: holiday,
        OvertimeMaxHoursPerDay: 4m, OvertimeMaxHoursPerMonth: 40m, OvertimeMaxHoursPerYear: 300,
        LegalReference: "BLLĐ 2019 Điều 98");

    private static IOvertimeMultiplierCalculator Multiplier() =>
        new OvertimeMultiplierCalculator(Mock.Of<IComplianceLookupService>());

    private static OvertimePayCalculator.OvertimeSegment Seg(
        DayType dayType, decimal dayHours, decimal nightHours, bool hasPrevDayOt = false) =>
        new(AnyDay, dayType, dayHours, nightHours, hasPrevDayOt);

    private static (decimal Full, decimal Premium) Run(params OvertimePayCalculator.OvertimeSegment[] segs) =>
        OvertimePayCalculator.Accumulate(segs, HourlyRate, Multiplier(), LaborLawTimeline.Uniform(Labor()));

    [Fact]
    public void NormalDay_MatchesOldHardcodedRates_NoRegression()
    {
        // Ngày thường: hệ số động PHẢI ra đúng 1.5 (ngày) / 2.0 (đêm) như code cũ.
        var (full, premium) = Run(Seg(DayType.Normal, dayHours: 2m, nightHours: 1m));

        // full = 50k×(2×1.5 + 1×2.0) = 50k×5 = 250k ; premium = 50k×(2×0.5 + 1×1.0) = 50k×2 = 100k
        full.Should().Be(250_000m);
        premium.Should().Be(100_000m);
    }

    [Fact]
    public void NormalNight_AfterDayOt_Uses210_Not200()
    {
        // Case 3 (Đ.98.3): đêm ngày thường ĐỨNG SAU giờ ngày OT = 150 + 30 + 20%×150 = 210%.
        var (full, premium) = Run(Seg(DayType.Normal, dayHours: 2m, nightHours: 1m, hasPrevDayOt: true));

        // full = 50k×(2×1.5 + 1×2.1) = 50k×5.1 = 255k ; premium = 50k×(2×0.5 + 1×1.1) = 50k×2.1 = 105k
        full.Should().Be(255_000m);
        premium.Should().Be(105_000m);
    }

    [Fact]
    public void PublicHoliday_UsesTripleRate_NotHardcoded()
    {
        // BS-2: OT 4 giờ ban ngày vào NGÀY LỄ phải 300%, không phải 150%.
        var (full, premium) = Run(Seg(DayType.PublicHoliday, dayHours: 4m, nightHours: 0m));

        // Đúng: 50k×4×3.0 = 600k (cũ sai: 50k×4×1.5 = 300k — trả một nửa).
        full.Should().Be(600_000m);
        premium.Should().Be(400_000m); // 50k×4×(3.0−1) = 400k miễn thuế
    }

    [Fact]
    public void WeeklyRestDay_UsesDoubleRate()
    {
        var (full, _) = Run(Seg(DayType.WeeklyRestDay, dayHours: 3m, nightHours: 0m));

        full.Should().Be(300_000m); // 50k×3×2.0
    }

    [Fact]
    public void HolidayNight_UsesConfiguredNightUplift()
    {
        // Đêm ngày lễ: 300 + 30 + 20%×300 = 390%.
        var (full, _) = Run(Seg(DayType.PublicHoliday, dayHours: 0m, nightHours: 2m));

        full.Should().Be(390_000m); // 50k×2×3.9
    }

    [Fact]
    public void ZeroHourlyRate_ReturnsZero()
    {
        var result = OvertimePayCalculator.Accumulate(
            new[] { Seg(DayType.PublicHoliday, 4m, 0m) },
            hourlyRate: 0m, Multiplier(), LaborLawTimeline.Uniform(Labor()));

        result.Should().Be((0m, 0m));
    }

    // ===== [Mục 1] Luật đổi GIỮA KỲ — mỗi ngày ăn hệ số của luật đang hiệu lực NGÀY ĐÓ =====

    private const int LawChangeDay = 15;
    private static readonly DateOnly BeforeChange = new(2026, 6, 14);
    private static readonly DateOnly OnChange = new(2026, 6, 15);

    /// <summary>Kỳ 06/2026: tới 14/06 sàn lễ 300%, từ 15/06 sàn lễ 400%.</summary>
    private static LaborLawTimeline SplitJune()
    {
        var oldLaw = Labor(versionId: 1, holiday: 300m);
        var newLaw = Labor(versionId: 2, holiday: 400m, validFromDay: LawChangeDay);
        var byDate = new Dictionary<DateOnly, LaborLawConstantsSnapshot>();
        for (var d = new DateOnly(2026, 6, 1); d <= new DateOnly(2026, 7, 1); d = d.AddDays(1))
            byDate[d] = d.Day >= LawChangeDay || d.Month == 7 ? newLaw : oldLaw;
        return new LaborLawTimeline(byDate, newLaw);
    }

    [Fact]
    public void LuatDoiGiuaKy_OtNgayLeTruocMoc_VanAnHeSoCu()
    {
        // OT 4h ban ngày, ngày LỄ 14/06 — trước mốc hiệu lực ⇒ vẫn 300%, KHÔNG bị áp hồi tố 400%.
        var (full, _) = OvertimePayCalculator.Accumulate(
            new[] { new OvertimePayCalculator.OvertimeSegment(BeforeChange, DayType.PublicHoliday, 4m, 0m) },
            HourlyRate, Multiplier(), SplitJune());

        full.Should().Be(600_000m); // 50k × 4 × 3.0
    }

    [Fact]
    public void LuatDoiGiuaKy_OtNgayLeTuMocTroDi_AnHeSoMoi()
    {
        // Cùng 4h ban ngày ngày LỄ nhưng rơi ĐÚNG ngày hiệu lực 15/06 ⇒ 400%.
        var (full, _) = OvertimePayCalculator.Accumulate(
            new[] { new OvertimePayCalculator.OvertimeSegment(OnChange, DayType.PublicHoliday, 4m, 0m) },
            HourlyRate, Multiplier(), SplitJune());

        full.Should().Be(800_000m); // 50k × 4 × 4.0
    }

    [Fact]
    public void LuatDoiGiuaKy_HaiNgayHaiBenMoc_CongDonTheoTungMuc()
    {
        // Một NLĐ tăng ca cả hai bên mốc: tổng phải là 600k + 800k, không phải 2 lần cùng một hệ số.
        var (full, _) = OvertimePayCalculator.Accumulate(
            new[]
            {
                new OvertimePayCalculator.OvertimeSegment(BeforeChange, DayType.PublicHoliday, 4m, 0m),
                new OvertimePayCalculator.OvertimeSegment(OnChange, DayType.PublicHoliday, 4m, 0m),
            },
            HourlyRate, Multiplier(), SplitJune());

        full.Should().Be(1_400_000m);
    }

    [Fact]
    public void KhongDoiLuat_KetQuaGiongHetDuongMotSnapshot_KhongHoiQuy()
    {
        // Kỳ không có đổi luật (đường phổ biến) phải ra đúng số cũ — mục 1 không được đổi số của ai khác.
        var segs = new[]
        {
            new OvertimePayCalculator.OvertimeSegment(BeforeChange, DayType.PublicHoliday, 4m, 0m),
            new OvertimePayCalculator.OvertimeSegment(OnChange, DayType.Normal, 2m, 1m),
        };

        var viaTimeline = OvertimePayCalculator.Accumulate(
            segs, HourlyRate, Multiplier(), LaborLawTimeline.Uniform(Labor()));

        viaTimeline.Full.Should().Be(600_000m + 250_000m);
    }
}
