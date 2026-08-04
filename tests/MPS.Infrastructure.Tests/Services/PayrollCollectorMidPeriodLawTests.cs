using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Domain.Enums.Payroll;
using static MPS.Application.Common.Payroll.PayrollFieldKeys;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// ⭐ [Mục 1] Luật lao động đổi GIỮA KỲ — tăng ca, phụ trội ngày nghỉ/lễ và phụ cấp làm đêm phải tính
/// theo luật của TỪNG NGÀY CÔNG, không áp hồi tố mức cuối tháng cho cả tháng.
///
/// <para>Kỳ chuẩn 07/2026 (xem <see cref="PayrollCollectorFixture"/>): 01/07 là Thứ Tư, các Thứ Bảy
/// 04/11/18/25. Mốc đổi luật dùng chung: <b>16/07/2026</b>. Hai ngày đo là 10/07 (Thứ Sáu) và
/// 20/07 (Thứ Hai) — cùng là NGÀY THƯỜNG nên chênh lệch duy nhất giữa chúng là bộ hằng số luật.</para>
///
/// <para>Đơn giá giờ = 10.000.000 / (26 × 8) = 48.076,923…đ — cố ý không tròn để test bắt được cả
/// sai lệch làm tròn.</para>
/// </summary>
public sealed class PayrollCollectorMidPeriodLawTests
{
    private static readonly DateOnly ChangeDate = new(2026, 7, 16);
    private const int DayBefore = 10;   // Thứ Sáu — trước mốc
    private const int DayAfter = 20;    // Thứ Hai — từ mốc trở đi

    /// <summary>Luật MỚI: uplift OT ngày thường 50% → 100%, phụ cấp đêm 30% → 50%.</summary>
    private static LaborLawConstantsSnapshot NewLaw => FakeComplianceLookup.BaseLabor with
    {
        VersionId = 2,
        ValidFrom = ChangeDate,
        OvertimeNormalUpliftPercent = 100m,
        NightWorkSurchargePercent = 50m,
        LegalReference = "NĐ 99/2026",
    };

    /// <summary>Lookup trả luật cũ cho ngày &lt; 16/07, luật mới từ 16/07 trở đi.</summary>
    private static FakeComplianceLookup SplitLookup() => new()
    {
        LaborLawByDate = date => date >= ChangeDate ? NewLaw : FakeComplianceLookup.BaseLabor,
    };

    private static decimal Round(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);

    // ===== Tăng ca =====

    [Fact]
    public async Task TangCa_NgayTruocMoc_AnHeSoCu_KhongBiApHoiTo()
    {
        // OT 4h ban ngày, ngày THƯỜNG 10/07 — trước mốc ⇒ 150% (100 + uplift 50).
        var db = await SeedBaseAsync();
        db.OvertimeRequests.Add(ApprovedOt(1, DayBefore, new TimeOnly(18, 0), new TimeOnly(22, 0)));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db, SplitLookup());

        inputs.Numbers[OtTangCa].Should().Be(Round(HourlyRate * 4m * 1.5m));
    }

    [Fact]
    public async Task TangCa_NgayTuMocTroDi_AnHeSoMoi()
    {
        // Cùng 4h ban ngày ngày THƯỜNG nhưng 20/07 ⇒ 200% (100 + uplift 100).
        var db = await SeedBaseAsync();
        db.OvertimeRequests.Add(ApprovedOt(1, DayAfter, new TimeOnly(18, 0), new TimeOnly(22, 0)));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db, SplitLookup());

        inputs.Numbers[OtTangCa].Should().Be(Round(HourlyRate * 4m * 2.0m));
    }

    [Fact]
    public async Task TangCa_HaiNgayHaiBenMoc_CongDonTheoTungMuc_KhongDungMotHeSo()
    {
        // Đây là ca chứng minh trọng tâm: cùng một NLĐ, cùng số giờ, hai bên mốc ⇒ tổng phải là
        // 150% + 200%, KHÔNG phải 2×200% (áp hồi tố luật mới) cũng không phải 2×150%.
        var db = await SeedBaseAsync();
        db.OvertimeRequests.Add(ApprovedOt(1, DayBefore, new TimeOnly(18, 0), new TimeOnly(22, 0)));
        db.OvertimeRequests.Add(ApprovedOt(2, DayAfter, new TimeOnly(18, 0), new TimeOnly(22, 0)));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db, SplitLookup());

        var expected = Round(HourlyRate * 4m * 1.5m) + Round(HourlyRate * 4m * 2.0m);
        inputs.Numbers[OtTangCa].Should().Be(expected);
        inputs.Numbers[OtTangCa].Should().NotBe(Round(HourlyRate * 8m * 2.0m), "không được áp hồi tố luật mới cho ngày 10/07");
    }

    [Fact]
    public async Task ChenhLechMienThue_CungTachTheoNgay()
    {
        // Phần vượt 100% (miễn thuế TNCN) phải theo cùng hệ số của chính ngày đó: 50% và 100%.
        var db = await SeedBaseAsync();
        db.OvertimeRequests.Add(ApprovedOt(1, DayBefore, new TimeOnly(18, 0), new TimeOnly(22, 0)));
        db.OvertimeRequests.Add(ApprovedOt(2, DayAfter, new TimeOnly(18, 0), new TimeOnly(22, 0)));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db, SplitLookup());

        var expected = Round(HourlyRate * 4m * 0.5m) + Round(HourlyRate * 4m * 1.0m);
        inputs.Numbers[OtChenhLech].Should().Be(expected);
    }

    // ===== Phụ cấp làm đêm cho giờ THƯỜNG =====

    [Fact]
    public async Task PhuCapDem_GioThuong_TachTheoTungNgay()
    {
        // Ca chính có giờ đêm ở cả hai bên mốc: 30% cho 10/07 + 50% cho 20/07.
        var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(DayBefore, dayHours: 4m, nightHours: 4m));
        db.AttendanceRecords.Add(ShiftRecord(DayAfter, dayHours: 4m, nightHours: 4m));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db, SplitLookup());

        var expected = Round(4m * HourlyRate * 0.30m + 4m * HourlyRate * 0.50m);
        inputs.Numbers[PremiumNight].Should().Be(expected);
    }

    [Fact]
    public async Task PhuCapDem_KhongDoiLuat_GiuNguyenSoCu_KhongHoiQuy()
    {
        // Kỳ chỉ có MỘT mức luật (đường phổ biến) phải ra đúng số như trước mục 1.
        var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(DayBefore, dayHours: 4m, nightHours: 4m));
        db.AttendanceRecords.Add(ShiftRecord(DayAfter, dayHours: 4m, nightHours: 4m));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);   // lookup mặc định = một mức cho cả kỳ

        inputs.Numbers[PremiumNight].Should().Be(Round(8m * HourlyRate * 0.30m));
    }

    // ===== Ghi chú trên bảng lương =====

    [Fact]
    public async Task GhiChu_LuatDoiGiuaKy_XuatHienVaNeuRoMoc()
    {
        var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(DayBefore));
        await db.SaveChangesAsync();

        var result = await CollectFullAsync(db, SplitLookup());

        var note = result.ComplianceNotes.Should().ContainSingle().Subject;
        note.NoteType.Should().Be(PayrollNoteType.LawChange);
        note.VersionType.Should().Be(ComplianceVersionType.LaborLawConstants);
        note.Body.Should().Contain("16/07/2026").And.Contain("NĐ 99/2026");
        note.AutoGenerated.Should().BeTrue();
    }

    [Fact]
    public async Task GhiChu_KyChiMotMucLuat_KhongSinhGhiChuThua()
    {
        var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(DayBefore));
        await db.SaveChangesAsync();

        var result = await CollectFullAsync(db);

        result.ComplianceNotes.Should().BeEmpty();
    }

    // ===== Ranh giới cố ý: đại lượng "cả kỳ" KHÔNG chia theo ngày =====

    [Fact]
    public async Task DonGiaGio_VanTheoMocCuoiKy_DuLuatDoiGiuaKy()
    {
        // `StandardWorkingHoursPerDay` không đổi giữa 2 mức nên đơn giá giờ phải y hệt đường một mức
        // ⇒ chứng minh mục 1 KHÔNG lén đổi cách tính đơn giá giờ (vốn là một con số cho cả bảng lương).
        var db = await SeedBaseAsync();
        db.OvertimeRequests.Add(ApprovedOt(1, DayBefore, new TimeOnly(18, 0), new TimeOnly(22, 0)));
        await db.SaveChangesAsync();

        var split = await CollectAsync(db, SplitLookup());

        var dbSingle = await SeedBaseAsync();
        dbSingle.OvertimeRequests.Add(ApprovedOt(1, DayBefore, new TimeOnly(18, 0), new TimeOnly(22, 0)));
        await dbSingle.SaveChangesAsync();
        var single = await CollectAsync(dbSingle);

        split.Numbers[OtTangCa].Should().Be(single.Numbers[OtTangCa]);
    }
}
