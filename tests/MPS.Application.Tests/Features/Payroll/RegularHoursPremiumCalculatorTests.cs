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
    /// <summary>Ngày mặc định cho các ca không quan tâm tới mốc đổi luật.</summary>
    private static readonly DateOnly AnyDay = new(2026, 6, 10);

    private static LaborLawConstantsSnapshot Labor(
        decimal normal = 100m, decimal weeklyRest = 200m, decimal holiday = 300m, decimal nightSurcharge = 30m,
        int versionId = 1) =>
        new(VersionId: versionId, ValidFrom: new DateOnly(2021, 1, 1),
            ProbationSalaryMinPercent: 85m, OvertimeNightExtraPercent: 20m,
            NightWorkSurchargePercent: nightSurcharge, OvertimeNormalUpliftPercent: 50m,
            NightStartTime: new TimeOnly(22, 0), NightEndTime: new TimeOnly(6, 0),
            StandardWorkingHoursPerDay: 8m, MaxRegularHoursPerWeek: 48m, StandardWorkingDaysPerMonth: 26m,
            AnnualLeaveMinDays: 12,
            WorkRateNormalFloor: normal, WorkRateWeeklyRestFloor: weeklyRest, WorkRateHolidayFloor: holiday,
            OvertimeMaxHoursPerDay: 4m, OvertimeMaxHoursPerMonth: 40m, OvertimeMaxHoursPerYear: 300,
            LegalReference: "BLLĐ 2019 Đ.98");

    private static RegularHoursPremiumCalculator.WorkDaySegment Seg(
        DayType dayType, decimal dayHours, decimal nightHours) => new(AnyDay, dayType, dayHours, nightHours);

    private static RegularHoursPremiumCalculator.WorkDaySegment SegOn(
        DateOnly date, DayType dayType, decimal dayHours, decimal nightHours) =>
        new(date, dayType, dayHours, nightHours);

    private static RegularHoursPremiumCalculator.Result Run(
        LaborLawConstantsSnapshot labor,
        params RegularHoursPremiumCalculator.WorkDaySegment[] segments) =>
        RegularHoursPremiumCalculator.Accumulate(segments, HourlyRate, LaborLawTimeline.Uniform(labor));

    [Fact]
    public void NgayThuong_KhongPhuTroi()
    {
        var result = Run(Labor(), Seg(DayType.Normal, 8m, 0m));

        result.DayTypePremium.Should().Be(0m);
        result.NightPremium.Should().Be(0m);
    }

    [Fact]
    public void NgayNghiTuan_TraThemPhanConThieu_100PhanTram_TongThanhToan200PhanTram()
    {
        // Ca 8h ngày CN: cột Thu nhập đã trả 100% ⇒ phụ trội = 8 × 50k × (200%−100%) = 400k.
        var result = Run(Labor(), Seg(DayType.WeeklyRestDay, 8m, 0m));

        result.DayTypePremium.Should().Be(400_000m);
    }

    [Fact]
    public void NgayLe_TraThemNguyen300PhanTram_TongThanhToan400PhanTram()
    {
        // ⭐ HOLIDAY-400-2026-08 — 100% cột Thu nhập đã trả là tiền của QUYỀN NGHỈ LỄ (Đ.112.1,
        // ở nhà vẫn được 1 công qua CONGLE), không phải tiền công của việc đi làm ⇒ không được
        // trừ vào mức 300% mà Đ.98.1c đòi. Phụ trội = 8 × 50k × 300% = 1,2tr; tổng ngày đó 400%.
        var result = Run(Labor(), Seg(DayType.PublicHoliday, 8m, 0m));

        result.DayTypePremium.Should().Be(1_200_000m);
    }

    [Fact]
    public void NgayNghiCoHuongLuong_TinhNhuNgayLe()
    {
        var result = Run(Labor(), Seg(DayType.PaidLeaveDay, 8m, 0m));

        result.DayTypePremium.Should().Be(1_200_000m,
            "Đ.98.1c gộp ngày lễ và ngày nghỉ có hưởng lương vào cùng một mức 300%");
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

        result.DayTypePremium.Should().Be(1_200_000m);  // +300%
        result.NightPremium.Should().Be(120_000m);      // +30%
    }

    [Fact]
    public void NhieuNgay_CongDon()
    {
        var result = Run(Labor(),
            Seg(DayType.Normal, 8m, 0m),
            Seg(DayType.WeeklyRestDay, 4m, 4m),
            Seg(DayType.PublicHoliday, 8m, 0m));

        // Nghỉ tuần: 8h × 50k × 100% = 400k; lễ: 8h × 50k × 300% = 1,2tr.
        result.DayTypePremium.Should().Be(1_600_000m);
        // Chỉ 4h đêm (ngày nghỉ tuần): 4 × 50k × 30% = 60k.
        result.NightPremium.Should().Be(60_000m);
    }

    [Fact]
    public void KhongCoLuong_KhongTinhPhuTroi()
    {
        var result = RegularHoursPremiumCalculator.Accumulate(
            new[] { Seg(DayType.PublicHoliday, 8m, 8m) },
            hourlyRate: 0m, LaborLawTimeline.Uniform(Labor()));

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

        current.DayTypePremium.Should().Be(1_200_000m);   // 8 × 50k × 300%
        updated.DayTypePremium.Should().Be(1_600_000m);   // 8 × 50k × 400%

        current.NightPremium.Should().Be(120_000m);
        updated.NightPremium.Should().Be(160_000m);       // 8 × 50k × 40%
    }

    // ===== [Mục 1] Luật đổi GIỮA KỲ — phụ trội/phụ cấp đêm tra theo NGÀY CÔNG =====

    private static readonly DateOnly BeforeChange = new(2026, 6, 14);
    private static readonly DateOnly OnChange = new(2026, 6, 15);

    /// <summary>Kỳ 06/2026: tới 14/06 lễ 300% + đêm 30%, từ 15/06 lễ 400% + đêm 40%.</summary>
    private static LaborLawTimeline SplitJune()
    {
        var oldLaw = Labor(versionId: 1);
        var newLaw = Labor(holiday: 400m, nightSurcharge: 40m, versionId: 2);
        var byDate = new Dictionary<DateOnly, LaborLawConstantsSnapshot>();
        for (var d = new DateOnly(2026, 6, 1); d <= new DateOnly(2026, 7, 1); d = d.AddDays(1))
            byDate[d] = d.Month == 7 || d.Day >= 15 ? newLaw : oldLaw;
        return new LaborLawTimeline(byDate, newLaw);
    }

    [Fact]
    public void LuatDoiGiuaKy_CaLeTruocMoc_GiuSanCu()
    {
        // Ca 8h ngày LỄ 14/06: sàn cũ 300% ⇒ phụ trội 8 × 50k × 300% = 1,2tr (không phải 1,6tr).
        var result = RegularHoursPremiumCalculator.Accumulate(
            new[] { SegOn(BeforeChange, DayType.PublicHoliday, 8m, 0m) }, HourlyRate, SplitJune());

        result.DayTypePremium.Should().Be(1_200_000m);
    }

    [Fact]
    public void LuatDoiGiuaKy_CaLeTuMocTroDi_AnSanMoi()
    {
        // Cùng ca 8h ngày LỄ nhưng 15/06: sàn mới 400% ⇒ 8 × 50k × 400% = 1,6tr.
        var result = RegularHoursPremiumCalculator.Accumulate(
            new[] { SegOn(OnChange, DayType.PublicHoliday, 8m, 0m) }, HourlyRate, SplitJune());

        result.DayTypePremium.Should().Be(1_600_000m);
    }

    [Fact]
    public void LuatDoiGiuaKy_PhuCapDem_TinhTheoTungNgay()
    {
        // 8h đêm ngày thường mỗi bên mốc: 30% cho 14/06 + 40% cho 15/06 = 120k + 160k.
        var result = RegularHoursPremiumCalculator.Accumulate(
            new[]
            {
                SegOn(BeforeChange, DayType.Normal, 0m, 8m),
                SegOn(OnChange, DayType.Normal, 0m, 8m),
            },
            HourlyRate, SplitJune());

        result.NightPremium.Should().Be(280_000m);
        result.DayTypePremium.Should().Be(0m);
    }
}
