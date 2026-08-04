using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// [Mục 2] Trộn tỷ lệ + trần bảo hiểm theo trọng số NGÀY LỊCH của kỳ.
///
/// <para>Kỳ mẫu: tháng 30 ngày, mốc đổi văn bản ngày 15 ⇒ <b>14 ngày mức cũ + 16 ngày mức mới</b>.</para>
/// </summary>
public sealed class InsurancePeriodBlenderTests
{
    private const int PeriodDays = 30;
    private const int OldDays = 14;   // ngày 1..14
    private const int NewDays = 16;   // ngày 15..30

    private static SocialInsuranceRateSnapshot Si(
        int versionId, decimal employeeSocial, decimal employerSocial,
        decimal health = 1.5m, decimal unemployment = 1m, decimal union = 2m,
        decimal referenceAmount = 2_340_000m, int socialCap = 20, int healthCap = 20, int unemploymentCap = 20,
        string legalRef = "VB cũ") => new(
        VersionId: versionId, ValidFrom: new DateOnly(2026, 6, versionId == 1 ? 1 : 15),
        EmployeeSocialPercent: employeeSocial, EmployerSocialPercent: employerSocial,
        EmployerOccupationalDiseasePercent: 0.5m,
        EmployeeHealthPercent: health, EmployerHealthPercent: 3m,
        EmployeeUnemploymentPercent: unemployment, EmployerUnemploymentPercent: 1m,
        EmployerUnionFundPercent: union,
        ReferenceAmount: referenceAmount, SocialCapMultiplier: socialCap,
        HealthCapMultiplier: healthCap, UnemploymentCapMultiplier: unemploymentCap,
        LegalReference: legalRef,
        Funds: new[] { new SocialInsuranceFundEntrySnapshot("HT", "Hưu trí", employeeSocial, employerSocial) },
        EligibilityMode: "ByContractSubtype", EligibleContractSubtypes: Array.Empty<string>(),
        MinTenureMonths: null);

    private static MinimumWageSnapshot Mw(int versionId, decimal amount, string legalRef = "LTT cũ") => new(
        VersionId: versionId, ValidFrom: new DateOnly(2026, 6, versionId == 1 ? 1 : 15),
        AppliedRegionCode: "I",
        Regions: new[] { new MinimumWageRegionSnapshot("I", amount) },
        LegalReference: legalRef);

    /// <summary>Dựng danh sách ngày: <paramref name="oldDays"/> ngày mức cũ rồi phần còn lại mức mới.</summary>
    private static List<InsurancePeriodBlender.DayRates> Days(
        SocialInsuranceRateSnapshot oldSi, MinimumWageSnapshot oldMw,
        SocialInsuranceRateSnapshot newSi, MinimumWageSnapshot newMw,
        int oldDays = OldDays, int totalDays = PeriodDays)
    {
        var list = new List<InsurancePeriodBlender.DayRates>(totalDays);
        for (var i = 0; i < totalDays; i++)
            list.Add(i < oldDays
                ? new InsurancePeriodBlender.DayRates(oldSi, oldMw)
                : new InsurancePeriodBlender.DayRates(newSi, newMw));
        return list;
    }

    private static List<InsurancePeriodBlender.DayRates> Uniform(
        SocialInsuranceRateSnapshot si, MinimumWageSnapshot mw, int totalDays = PeriodDays) =>
        Days(si, mw, si, mw, oldDays: totalDays, totalDays: totalDays);

    // ===== Không đổi văn bản: phải ra ĐÚNG số cũ =====

    [Fact]
    public void KhongDoiVanBan_TyLeBangDungSoCua_MotVanBan_KhongHoiQuy()
    {
        var si = Si(1, employeeSocial: 8m, employerSocial: 17m);
        var rates = InsurancePeriodBlender.Blend(Uniform(si, Mw(1, 4_960_000m)));

        rates.BhxhNldRate.Should().Be(0.08m);
        rates.BhxhDnRate.Should().Be(0.17m);
        rates.BhxhBhytCap.Should().Be(2_340_000m * 20);
        rates.BhtnCap.Should().Be(4_960_000m * 20);
        rates.RegionMinWage.Should().Be(4_960_000m);
        rates.ChangedWithinPeriod.Should().BeFalse();
    }

    // ===== Đổi giữa kỳ: bình quân theo ngày =====

    [Fact]
    public void DoiTyLeGiuaKy_BinhQuanTheoSoNgay_KhongPhaiLayMotBen()
    {
        // NLĐ: 8% cho 14 ngày, 10% cho 16 ngày ⇒ (14×8 + 16×10)/30 = 272/30 = 9,0666…%
        var rates = InsurancePeriodBlender.Blend(Days(
            Si(1, 8m, 17m), Mw(1, 4_960_000m),
            Si(2, 10m, 17m), Mw(1, 4_960_000m)));

        var expected = (OldDays * 8m + NewDays * 10m) / PeriodDays / 100m;
        rates.BhxhNldRate.Should().Be(expected);
        rates.BhxhNldRate.Should().BeGreaterThan(0.08m).And.BeLessThan(0.10m);
        rates.ChangedWithinPeriod.Should().BeTrue();
    }

    [Fact]
    public void DoiTranGiuaKy_BinhQuanTheoSoNgay()
    {
        // Mức tham chiếu 2.340.000 → 2.600.000 từ ngày 15, hệ số trần giữ 20.
        var rates = InsurancePeriodBlender.Blend(Days(
            Si(1, 8m, 17m), Mw(1, 4_960_000m),
            Si(2, 8m, 17m, referenceAmount: 2_600_000m), Mw(1, 4_960_000m)));

        var expected = (OldDays * 2_340_000m * 20 + NewDays * 2_600_000m * 20) / PeriodDays;
        rates.BhxhBhytCap.Should().Be(expected);
    }

    [Fact]
    public void DoiCaMucThamChieuVaHeSoTran_TronTrenTICH_KhongPhaiTichCuaHaiBinhQuan()
    {
        // Bẫy: nếu trộn riêng ReferenceAmount và Multiplier rồi mới nhân, kết quả sẽ là một con số
        // KHÔNG ứng với ngày nào. Phải trộn trên chính tích của từng ngày.
        var oldSi = Si(1, 8m, 17m, referenceAmount: 2_000_000m, socialCap: 20);
        var newSi = Si(2, 8m, 17m, referenceAmount: 3_000_000m, socialCap: 10);

        var rates = InsurancePeriodBlender.Blend(Days(oldSi, Mw(1, 4_960_000m), newSi, Mw(1, 4_960_000m)));

        var correct = (OldDays * 2_000_000m * 20 + NewDays * 3_000_000m * 10) / PeriodDays;
        var wrong = ((OldDays * 2_000_000m + NewDays * 3_000_000m) / PeriodDays)
                  * ((OldDays * 20m + NewDays * 10m) / PeriodDays);

        rates.BhxhBhytCap.Should().Be(correct);
        rates.BhxhBhytCap.Should().NotBe(wrong);
    }

    [Fact]
    public void DoiLuongToiThieuVung_KeoTheoTranBHTN_BinhQuanTheoNgay()
    {
        var rates = InsurancePeriodBlender.Blend(Days(
            Si(1, 8m, 17m), Mw(1, 4_960_000m),
            Si(1, 8m, 17m), Mw(2, 5_310_000m, "NĐ LTT mới")));

        rates.BhtnCap.Should().Be((OldDays * 4_960_000m * 20 + NewDays * 5_310_000m * 20) / PeriodDays);
        rates.RegionMinWage.Should().Be((OldDays * 4_960_000m + NewDays * 5_310_000m) / PeriodDays);
        rates.ChangedWithinPeriod.Should().BeTrue("lương tối thiểu vùng đổi cũng là đổi giữa kỳ");
    }

    [Fact]
    public void DoiDungNgayDauKy_ToanKyMucMoi_BinhQuanBangMucMoi()
    {
        // Ranh giới: mốc rơi vào ngày 1 ⇒ không có ngày nào mức cũ ⇒ bình quân = mức mới nguyên.
        var rates = InsurancePeriodBlender.Blend(Days(
            Si(1, 8m, 17m), Mw(1, 4_960_000m),
            Si(2, 10m, 17m), Mw(1, 4_960_000m), oldDays: 0));

        rates.BhxhNldRate.Should().Be(0.10m);
    }

    [Fact]
    public void MoiVersionChiGomMotLan_XepTheoNgayHieuLuc()
    {
        var rates = InsurancePeriodBlender.Blend(Days(
            Si(1, 8m, 17m), Mw(1, 4_960_000m),
            Si(2, 10m, 17m, legalRef: "VB mới"), Mw(1, 4_960_000m)));

        rates.SocialVersions.Should().HaveCount(2);
        rates.SocialVersions[0].VersionId.Should().Be(1);
        rates.SocialVersions[1].VersionId.Should().Be(2);
        rates.MinWageVersions.Should().ContainSingle();
    }

    [Fact]
    public void KyRong_NemLoi_KhongTraSoVoNghia()
    {
        var act = () => InsurancePeriodBlender.Blend(Array.Empty<InsurancePeriodBlender.DayRates>());

        act.Should().Throw<ArgumentException>();
    }

    // ===== Ghi chú =====

    [Fact]
    public void GhiChu_KhongDoiVanBan_KhongSinhGhiChu()
    {
        var rates = InsurancePeriodBlender.Blend(Uniform(Si(1, 8m, 17m), Mw(1, 4_960_000m)));

        MidPeriodComplianceNotes.ForInsurance(rates, PeriodDays, DateTime.UtcNow).Should().BeNull();
    }

    [Fact]
    public void GhiChu_CoTron_NoiRoLaBinhQuan_VaCanhBaoNhomChamTran()
    {
        var rates = InsurancePeriodBlender.Blend(Days(
            Si(1, 8m, 17m), Mw(1, 4_960_000m),
            Si(2, 10m, 17m, legalRef: "NĐ 88/2026"), Mw(1, 4_960_000m)));

        var note = MidPeriodComplianceNotes.ForInsurance(rates, PeriodDays, new DateTime(2026, 7, 1));

        note.Should().NotBeNull();
        note!.NoteType.Should().Be(PayrollNoteType.LawChange);
        note.VersionType.Should().Be(ComplianceVersionType.SocialInsurance);
        note.Body.Should().Contain("BÌNH QUÂN").And.Contain("30 ngày lịch");
        // Không được giấu sai số: kế toán phải biết nhóm chạm trần có thể lệch.
        note.Body.Should().Contain("CHẠM TRẦN");
        note.Body.Should().Contain("15/06/2026").And.Contain("NĐ 88/2026");
    }
}
