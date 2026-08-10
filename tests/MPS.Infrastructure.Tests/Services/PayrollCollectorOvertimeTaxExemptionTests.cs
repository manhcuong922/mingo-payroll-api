using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using static MPS.Application.Common.Payroll.PayrollFieldKeys;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// Phạm vi miễn thuế TNCN của tiền tăng ca — đổi theo Luật TNCN 109/2025/QH15 + NĐ 253/2026/NĐ-CP
/// (từ 01/07/2026): miễn TOÀN BỘ tiền tăng ca thay vì chỉ phần chênh lệch vượt 100% như TT 111/2013 Đ.3.
///
/// <para>Cột <c>OtChenhLech</c> là số bị TRỪ khỏi thu nhập chịu thuế (<c>TNCT = TổngThuNhập − … −
/// ChênhLệchLTG − …</c>), còn <c>OtTangCa</c> đã nằm trong Tổng thu nhập. Vậy "miễn toàn bộ" nghĩa là
/// hai cột phải BẰNG NHAU để tiền tăng ca triệt tiêu khỏi TNCT.</para>
///
/// <para>Nền đo: kỳ 07/2026, OT 4h ban ngày ngày THƯỜNG ⇒ hệ số 150% (đơn giá × 4 × 1.5), phần chênh
/// lệch của luật cũ là 50% (đơn giá × 4 × 0.5).</para>
/// </summary>
public sealed class PayrollCollectorOvertimeTaxExemptionTests
{
    private const int WorkDay = 10;   // 10/07/2026 — Thứ Sáu, ngày thường

    private static decimal Round(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);

    private static FakeComplianceLookup LookupWith(bool fullyExempt) => new()
    {
        LaborLawByDate = _ => FakeComplianceLookup.BaseLabor with
        {
            OvertimeIncomeFullyTaxExempt = fullyExempt,
        },
    };

    private static async Task<MPS.Application.Common.Payroll.PayrollEmployeeInputs> CollectWithOtAsync(
        bool fullyExempt)
    {
        var db = await SeedBaseAsync();
        db.OvertimeRequests.Add(ApprovedOt(1, WorkDay, new TimeOnly(18, 0), new TimeOnly(22, 0)));
        db.AttendanceRecords.Add(OtRecord(WorkDay, 1, new TimeOnly(18, 0), new TimeOnly(22, 0)));
        await db.SaveChangesAsync();

        return await CollectAsync(db, LookupWith(fullyExempt));
    }

    [Fact]
    public async Task LuatCu_ChiMienPhanChenhLech_VuotTren100PhanTram()
    {
        var inputs = await CollectWithOtAsync(fullyExempt: false);

        inputs.Numbers[OtTangCa].Should().Be(Round(HourlyRate * 4m * 1.5m));
        inputs.Numbers[OtChenhLech].Should().Be(Round(HourlyRate * 4m * 0.5m));
    }

    [Fact]
    public async Task LuatMoi_MienToanBoTienTangCa_KhongChiPhanChenhLech()
    {
        var inputs = await CollectWithOtAsync(fullyExempt: true);

        var tangCa = Round(HourlyRate * 4m * 1.5m);
        inputs.Numbers[OtTangCa].Should().Be(tangCa);
        inputs.Numbers[OtChenhLech].Should()
            .Be(tangCa, "miễn toàn bộ ⇒ số trừ khỏi TNCT phải bằng đúng tiền tăng ca đã cộng vào Tổng thu nhập");
        inputs.Numbers[OtChenhLech].Should()
            .NotBe(Round(HourlyRate * 4m * 0.5m), "không được rơi lại về cách tính chỉ-miễn-chênh-lệch của luật cũ");
    }

    [Fact]
    public async Task KhongCoTangCa_CaHaiCheDoDeuBangKhong()
    {
        var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(WorkDay));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db, LookupWith(fullyExempt: true));

        inputs.Numbers[OtTangCa].Should().Be(0m);
        inputs.Numbers[OtChenhLech].Should().Be(0m);
    }
}
