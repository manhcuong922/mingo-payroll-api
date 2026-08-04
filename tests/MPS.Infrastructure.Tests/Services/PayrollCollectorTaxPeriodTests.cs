using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Payroll;
using static MPS.Application.Common.Payroll.PayrollFieldKeys;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// ⭐ [Mục 3] Thuế TNCN, giảm trừ bản thân/NPT và ngưỡng cơm miễn thuế CỐ Ý **không** chia theo ngày —
/// giữ tra tại một mốc duy nhất là <c>PayDate</c>.
///
/// <para><b>Vì sao đây là quyết định, không phải thiếu sót.</b> Thuế TNCN tính lũy tiến trên thu nhập
/// CẢ THÁNG và các bậc thuế là mốc theo tháng. Chẻ tháng ra tính thuế riêng từng nửa sẽ đẩy NLĐ xuống
/// bậc thấp hơn ⇒ tổng thuế khấu trừ LUÔN thấp hơn số thực phải nộp, quyết toán năm vênh và công ty
/// với tư cách bên khấu trừ chịu trách nhiệm. Giảm trừ gia cảnh và ngưỡng cơm cũng là mức theo tháng:
/// không văn bản nào cho phép cấp một con số ở giữa.</para>
///
/// <para>Bộ test này KHOÁ hành vi đó lại, để mục 1 (luật lao động theo ngày) và mục 2 (bảo hiểm bình
/// quân theo ngày) — hay bất kỳ thay đổi nào sau này — không vô tình kéo khối thuế vào diện chia.</para>
/// </summary>
public sealed class PayrollCollectorTaxPeriodTests
{
    private static readonly DateOnly ChangeDate = new(2026, 7, 16);

    /// <summary>Biểu thuế MỚI: giảm trừ bản thân 11tr → 15,5tr, NPT 4,4tr → 6,2tr.</summary>
    private static PitRegulationSnapshot NewPit => FakeComplianceLookup.BasePit with
    {
        VersionId = 2,
        ValidFrom = ChangeDate,
        PersonalDeductionAmount = 15_500_000m,
        DependentDeductionAmount = 6_200_000m,
        LegalReference = "NQ 110/2026",
    };

    private static FakeComplianceLookup SplitPit() => new()
    {
        PitByDate = d => d >= ChangeDate ? NewPit : FakeComplianceLookup.BasePit,
    };

    // ===== Giảm trừ giữ theo mốc cuối kỳ =====

    [Fact]
    public async Task GiamTruBanThan_LayTaiPayDate_KhongBinhQuanTheoNgay()
    {
        // PayDate của fixture = 05/08/2026, tức SAU mốc đổi 16/07 ⇒ phải lấy trọn mức MỚI 15,5tr.
        // Nếu ai đó lỡ đem giảm trừ vào diện bình quân, con số sẽ rơi vào khoảng giữa 11tr và 15,5tr.
        var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(10));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db, SplitPit());

        inputs.Numbers[PersonalDeduction].Should().Be(15_500_000m);
        inputs.Numbers[DependentDeduction].Should().Be(6_200_000m);
    }

    [Fact]
    public async Task ChiTraCuuThue_DUNG_MOT_LAN_TaiPayDate()
    {
        // Khoá thẳng vào THIẾT KẾ, không chỉ vào con số: collector không được fan-out biểu thuế theo
        // từng ngày của kỳ. Đây là hàng rào bền nhất chống việc vô tình "chia thuế theo ngày".
        var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(10));
        await db.SaveChangesAsync();

        var lookup = SplitPit();
        await CollectAsync(db, lookup);

        lookup.QueriedPitDates.Should().ContainSingle()
            .Which.Should().Be(PayDate);
    }

    [Fact]
    public async Task NguongComMienThue_CungLayTaiPayDate()
    {
        var db = await SeedBaseAsync();
        db.AllowanceTypes.Add(new AllowanceType { Id = 1, Code = "MEAL", Name = "Phụ cấp ăn ca" });
        db.AttendanceRecords.Add(ShiftRecord(10));
        await db.SaveChangesAsync();

        var oldThreshold = Threshold(1, 730_000m, new DateOnly(2026, 1, 1));
        var newThreshold = Threshold(2, 1_000_000m, ChangeDate);
        var lookup = new FakeComplianceLookup
        {
            AllowanceThresholdByDate = d => d >= ChangeDate ? newThreshold : oldThreshold,
        };

        var inputs = await CollectAsync(db, lookup);

        inputs.Numbers[MealExemptThreshold].Should().Be(1_000_000m);
        lookup.QueriedAllowanceThresholdDates.Should().ContainSingle()
            .Which.Should().Be(PayDate);
    }

    // ===== Ranh giới với mục 1 và mục 2 =====

    [Fact]
    public async Task BaoHiemHoiTheoTungNgay_ConThueChiHoiMotLan_RanhGioiRoRang()
    {
        // Cùng một lượt gom liệu: bảo hiểm phải hỏi đủ 31 ngày của tháng 07/2026, thuế đúng 1 lần.
        // Test này vỡ ngay nếu ai đó gộp hai nhóm vào chung một đường tra.
        var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(10));
        await db.SaveChangesAsync();

        var lookup = new FakeComplianceLookup();
        await CollectAsync(db, lookup);

        lookup.QueriedSocialInsuranceDates.Should().HaveCountGreaterThanOrEqualTo(31,
            "bảo hiểm [mục 2] tra theo từng ngày lịch của kỳ");
        lookup.QueriedPitDates.Should().ContainSingle(
            "thuế [mục 3] chỉ tra tại một mốc PayDate");
    }

    [Fact]
    public async Task KhongSinhGhiChuBinhQuanChoThue_DuBieuThueDoiGiuaKy()
    {
        // Thuế không chia ⇒ không được sinh ghi chú kiểu "đã bình quân" cho khối thuế; nếu có thì kế
        // toán sẽ tưởng thuế cũng bị chia, hiểu sai bản chất con số.
        var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(10));
        await db.SaveChangesAsync();

        var result = await CollectFullAsync(db, SplitPit());

        result.ComplianceNotes.Should().NotContain(n => n.VersionType == ComplianceVersionType.Pit);
    }

    private static AllowanceExemptThresholdSnapshot Threshold(int versionId, decimal amount, DateOnly validFrom) =>
        new(VersionId: versionId, ValidFrom: validFrom, AllowanceTypeId: 1,
            AllowanceTypeCode: "MEAL", AllowanceTypeName: "Phụ cấp ăn ca",
            ThresholdMode: "Monthly", MonthlyExemptAmount: amount,
            AppliesToTax: true, AppliesToSocial: false, LegalReference: "test");
}
