using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// BS-2 phần A — phụ trội giờ THƯỜNG theo loại ngày + phụ cấp làm đêm.
/// Mọi hệ số đọc từ <see cref="LaborLawConstantsSnapshot"/> (module Compliance) — test cuối cùng
/// khẳng định điều đó: đổi cấu hình compliance thì tiền lương đổi theo, không cần sửa code.
/// </summary>
public sealed class RegularHoursPremiumCalculatorTests
{
    private const decimal HourlyRate = 50_000m;

    // Hằng số luật hiện hành: thường 100%, nghỉ tuần 200%, lễ 300%, phụ cấp đêm 30%.
    private static LaborLawConstantsSnapshot Labor(
        decimal normal = 100m, decimal weeklyRest = 200m, decimal holiday = 300m, decimal nightSurcharge = 30m) =>
        new(VersionId: 1, ValidFrom: new DateOnly(2021, 1, 1),
            ProbationSalaryMinPercent: 85m, OvertimeNightExtraPercent: 20m,
            NightWorkSurchargePercent: nightSurcharge, OvertimeNormalUpliftPercent: 50m,
            NightStartTime: new TimeOnly(22, 0), NightEndTime: new TimeOnly(6, 0),
            StandardWorkingHoursPerDay: 8m, MaxRegularHoursPerWeek: 48m, StandardWorkingDaysPerMonth: 26m,
            AnnualLeaveMinDays: 12,
            WorkRateNormalFloor: normal, WorkRateWeeklyRestFloor: weeklyRest, WorkRateHolidayFloor: holiday,
            OvertimeMaxHoursPerDay: 4m, OvertimeMaxHoursPerMonth: 40m, OvertimeMaxHoursPerYear: 300,
            LegalReference: "BLLĐ 2019 Đ.98");

    private static RegularHoursPremiumCalculator.WorkDaySegment Seg(
        DayType dayType, decimal dayHours, decimal nightHours) => new(dayType, dayHours, nightHours);

    private static RegularHoursPremiumCalculator.Result Run(
        LaborLawConstantsSnapshot labor,
        params RegularHoursPremiumCalculator.WorkDaySegment[] segments) =>
        RegularHoursPremiumCalculator.Accumulate(segments, HourlyRate, labor);

    [Fact]
    public void NgayThuong_KhongPhuTroi()
    {
        var result = Run(Labor(), Seg(DayType.Normal, 8m, 0m));

        result.DayTypePremium.Should().Be(0m);
        result.NightPremium.Should().Be(0m);
    }

    [Fact]
    public void NgayNghiTuan_TraThemPhanConThieu_100PhanTram()
    {
        // Ca 8h ngày CN: cột Thu nhập đã trả 100% ⇒ phụ trội = 8 × 50k × (200%−100%) = 400k.
        var result = Run(Labor(), Seg(DayType.WeeklyRestDay, 8m, 0m));

        result.DayTypePremium.Should().Be(400_000m);
    }

    [Fact]
    public void NgayLe_TraThem200PhanTram()
    {
        // 8 × 50k × (300%−100%) = 800k.
        var result = Run(Labor(), Seg(DayType.PublicHoliday, 8m, 0m));

        result.DayTypePremium.Should().Be(800_000m);
    }

    [Fact]
    public void GioDem_NgayThuong_DuocPhuCap30PhanTram()
    {
        // Trước BS-2 khoản này KHÔNG được trả (không cột nào dùng NightHours).
        var result = Run(Labor(), Seg(DayType.Normal, 0m, 8m));

        result.DayTypePremium.Should().Be(0m);
        result.NightPremium.Should().Be(120_000m);   // 8 × 50k × 30%
    }

    [Fact]
    public void GioDem_NgayLe_CongCaHaiKhoan_DocLapNhau()
    {
        // Chốt PO: loại NGÀY và khung GIỜ là 2 yếu tố độc lập ⇒ cộng cả hai.
        var result = Run(Labor(), Seg(DayType.PublicHoliday, 0m, 8m));

        result.DayTypePremium.Should().Be(800_000m);  // +200%
        result.NightPremium.Should().Be(120_000m);    // +30%
    }

    [Fact]
    public void NhieuNgay_CongDon()
    {
        var result = Run(Labor(),
            Seg(DayType.Normal, 8m, 0m),
            Seg(DayType.WeeklyRestDay, 4m, 4m),
            Seg(DayType.PublicHoliday, 8m, 0m));

        // Nghỉ tuần: 8h × 50k × 100% = 400k; lễ: 8h × 50k × 200% = 800k.
        result.DayTypePremium.Should().Be(1_200_000m);
        // Chỉ 4h đêm (ngày nghỉ tuần): 4 × 50k × 30% = 60k.
        result.NightPremium.Should().Be(60_000m);
    }

    [Fact]
    public void KhongCoLuong_KhongTinhPhuTroi()
    {
        var result = RegularHoursPremiumCalculator.Accumulate(
            new[] { Seg(DayType.PublicHoliday, 8m, 8m) },
            hourlyRate: 0m, Labor());

        result.DayTypePremium.Should().Be(0m);
        result.NightPremium.Should().Be(0m);
    }

    [Fact]
    public void DoiCauHinhCompliance_TienLuongDoiTheo_KhongSuaCode()
    {
        // Kịch bản: nhà nước nâng sàn ngày lễ 300% → 400% và phụ cấp đêm 30% → 40%.
        // HR chỉ sửa ở module Compliance; payroll phải áp theo ngay.
        var current = Run(Labor(), Seg(DayType.PublicHoliday, 0m, 8m));
        var updated = Run(Labor(holiday: 400m, nightSurcharge: 40m),
            Seg(DayType.PublicHoliday, 0m, 8m));

        current.DayTypePremium.Should().Be(800_000m);
        updated.DayTypePremium.Should().Be(1_200_000m);   // 8 × 50k × (400%−100%)

        current.NightPremium.Should().Be(120_000m);
        updated.NightPremium.Should().Be(160_000m);       // 8 × 50k × 40%
    }
}
