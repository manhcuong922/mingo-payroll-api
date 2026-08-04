using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Enums.Payroll;
using static MPS.Application.Common.Payroll.PayrollFieldKeys;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// ⭐ [Mục 2] Bảo hiểm khi văn bản đổi GIỮA KỲ — tỷ lệ và trần đổ vào bảng lương là số BÌNH QUÂN
/// theo ngày lịch, KHÔNG phải số của một văn bản đơn lẻ và cũng không áp hồi tố mức cuối tháng.
///
/// <para>Kỳ chuẩn 07/2026: <b>31 ngày</b>. Mốc đổi văn bản 16/07 ⇒ 15 ngày mức cũ + 16 ngày mức mới.</para>
/// </summary>
public sealed class PayrollCollectorMidPeriodInsuranceTests
{
    private static readonly DateOnly ChangeDate = new(2026, 7, 16);
    private const int PeriodDays = 31;
    private const int OldDays = 15;   // 01..15
    private const int NewDays = 16;   // 16..31

    /// <summary>BHXH mới: NLĐ 8% → 10% (quỹ hưu trí), mức tham chiếu 2.340.000 → 2.600.000.</summary>
    private static SocialInsuranceRateSnapshot NewSi => FakeComplianceLookup.BaseSocialInsurance with
    {
        VersionId = 2,
        ValidFrom = ChangeDate,
        ReferenceAmount = 2_600_000m,
        LegalReference = "NĐ 88/2026",
        Funds = new[] { new SocialInsuranceFundEntrySnapshot("HT", "Hưu trí", 10m, 14m) },
    };

    private static MinimumWageSnapshot NewMinWage => FakeComplianceLookup.BaseMinimumWage with
    {
        VersionId = 2,
        ValidFrom = ChangeDate,
        Regions = new[] { new MinimumWageRegionSnapshot("I", 5_310_000m) },
        LegalReference = "NĐ LTT 2026",
    };

    private static FakeComplianceLookup SplitSocial() => new()
    {
        SocialInsuranceByDate = d => d >= ChangeDate ? NewSi : FakeComplianceLookup.BaseSocialInsurance,
    };

    private static FakeComplianceLookup SplitMinWage() => new()
    {
        MinimumWageByDate = d => d >= ChangeDate ? NewMinWage : FakeComplianceLookup.BaseMinimumWage,
    };

    // ===== Tỷ lệ =====

    [Fact]
    public async Task TyLeBhxhNld_DoiGiuaKy_RaSoBinhQuanTheoNgay()
    {
        var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(10));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db, SplitSocial());

        var expected = (OldDays * 8m + NewDays * 10m) / PeriodDays / 100m;
        inputs.Numbers[BhxhNldRate].Should().Be(expected);
        inputs.Numbers[BhxhNldRate].Should().BeGreaterThan(0.08m).And.BeLessThan(0.10m,
            "phải nằm GIỮA hai mức — không lấy trọn bên nào");
    }

    [Fact]
    public async Task TranBhxh_DoiGiuaKy_RaSoBinhQuanTheoNgay()
    {
        var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(10));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db, SplitSocial());

        var expected = (OldDays * 2_340_000m * 20 + NewDays * 2_600_000m * 20) / PeriodDays;
        inputs.Numbers[BhxhBhytCap].Should().Be(expected);
    }

    [Fact]
    public async Task TranBhtn_TheoLuongToiThieuVungDoiGiuaKy_BinhQuanTheoNgay()
    {
        var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(10));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db, SplitMinWage());

        var expected = (OldDays * 4_960_000m * 20 + NewDays * 5_310_000m * 20) / PeriodDays;
        inputs.Numbers[BhtnCap].Should().Be(expected);
    }

    [Fact]
    public async Task KhongDoiVanBan_MoiSoBaoHiemGiuNguyen_KhongHoiQuy()
    {
        // Đường phổ biến: kỳ chỉ một mức ⇒ bình quân phải bằng đúng số cũ, không lệch một đồng.
        var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(10));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[BhxhNldRate].Should().Be(0.08m);
        inputs.Numbers[BhxhDnRate].Should().Be(0.14m);
        inputs.Numbers[BhxhBhytCap].Should().Be(2_340_000m * 20);
        inputs.Numbers[BhtnCap].Should().Be(4_960_000m * 20);
    }

    // ===== Dấu vết đối chiếu =====

    [Fact]
    public async Task ComplianceRefs_GhiDU_CaHaiVanBanBaoHiem_KhongChiMotBen()
    {
        // Kế toán hỏi "số này căn cứ văn bản nào" phải nhận được CẢ HAI, không phải một nửa.
        var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(10));
        await db.SaveChangesAsync();

        var result = await CollectFullAsync(db, SplitSocial());

        var siRefs = result.ComplianceRefs
            .Where(r => r.VersionType == ComplianceVersionType.SocialInsurance).ToList();
        siRefs.Should().HaveCount(2);
        siRefs.Select(r => r.VersionId).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task GhiChu_CoTron_XuatHienVaNoiRoLaBinhQuan()
    {
        var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(10));
        await db.SaveChangesAsync();

        var result = await CollectFullAsync(db, SplitSocial());

        var note = result.ComplianceNotes
            .Should().ContainSingle(n => n.VersionType == ComplianceVersionType.SocialInsurance).Subject;
        note.Body.Should().Contain("BÌNH QUÂN").And.Contain("31 ngày lịch").And.Contain("NĐ 88/2026");
    }

    [Fact]
    public async Task GhiChu_KhongDoiVanBan_KhongMocGhiChuThua()
    {
        var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(10));
        await db.SaveChangesAsync();

        var result = await CollectFullAsync(db);

        result.ComplianceNotes.Should().BeEmpty();
    }

    // ===== Sai số đã biết ở nhóm chạm trần — đo thẳng để không ai phát hiện muộn =====

    [Fact]
    public async Task NguoiDuoiTran_KetQuaChinhXacTuyetDoi_BangTachDoiKyThat()
    {
        // Nền đóng (10tr) < trần ⇒ MIN không cắn ⇒ tiền BH bình quân = đúng tổng của hai nửa kỳ thật.
        var db = await SeedBaseAsync();
        for (var day = 1; day <= 26; day++) db.AttendanceRecords.Add(ShiftRecord(day));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db, SplitSocial());

        // Nền đóng = MUC_LUONG chia theo công (`ProrateByCong`) — giống hệt nhau ở cả hai cách tính,
        // nên chứng minh quy về: nền × tỷ_lệ_bình_quân == Σ(nền × tỷ_lệ_mỗi_ngày) / số_ngày.
        //
        // So bằng `Be` là sai ngưỡng: hai vế bằng nhau về toán học nhưng khác THỨ TỰ phép chia decimal
        // nên lệch ~1e-16. Ngưỡng đúng cho một khẳng định về TIỀN là dưới một xu.
        var blended = (OldDays * 8m + NewDays * 10m) / PeriodDays / 100m;
        inputs.Numbers[BhxhNldRate].Should().Be(blended);

        var viaBlendedRate = Salary * blended;
        var viaSplitPeriod = (OldDays * (Salary * 0.08m) + NewDays * (Salary * 0.10m)) / PeriodDays;
        viaBlendedRate.Should().BeApproximately(viaSplitPeriod, 0.01m,
            "người dưới trần thì trộn tỷ lệ tương đương tuyệt đối với tách đôi kỳ");
    }
}
