using FluentAssertions;
using MPS.Application.Common.Payroll;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// Phase 7 — collector đổ tỷ lệ thuế khoán + ngưỡng khấu trừ từ <c>PitRegulationVersion</c> xuống
/// bảng công thức. Phần CÔNG THỨC (dưới ngưỡng ⇒ 0, kỳ cũ ⇒ fallback 10%) đo ở
/// <c>MPS.Application.Tests.Features.Payroll.FlatRateTaxThresholdFormulaTests</c>.
/// </summary>
public sealed class PayrollCollectorFlatTaxComplianceTests
{
    [Fact(DisplayName = "Phase 7 · Tỷ lệ thuế khoán đổ xuống dạng TỶ LỆ, không phải phần trăm")]
    public async Task FlatRate_IsConvertedFromPercentToFraction()
    {
        using var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        // Compliance lưu 10.00 (phần trăm) còn công thức nhân thẳng vào tổng thu nhập ⇒ phải là
        // 0.10. Quên chia 100 là thu thuế gấp 10 lần, cùng quy ước với TYLE_BHXH/TYLE_KPCD.
        inputs.Numbers[PayrollFieldKeys.ShortTermFlatRate].Should().Be(0.10m);
    }

    [Fact(DisplayName = "Phase 7 · Ngưỡng khấu trừ đổ xuống nguyên số tiền của bản luật")]
    public async Task Threshold_ComesFromPitVersion()
    {
        using var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ShortTermTaxExemptIncome].Should().Be(2_000_000m,
            "fixture đang dùng bản luật cũ (ngưỡng 2tr)");
    }

    [Fact(DisplayName = "Phase 7 · Đổi bản luật ⇒ ngưỡng/tỷ lệ mới đi thẳng vào kỳ lương")]
    public async Task NewPitVersion_FlowsIntoPeriod()
    {
        using var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        // NĐ 253/2026 từ 01/07/2026: ngưỡng 5tr. Kỳ lương lấy bản luật theo NGÀY TRẢ nên kỳ cũ
        // (trả trước 01/07/2026) vẫn ăn bản 2tr — đó là lý do dựng version mới thay vì sửa tại chỗ.
        var compliance = new FakeComplianceLookup
        {
            PitByDate = _ => FakeComplianceLookup.BasePit with
            {
                ShortTermFlatRatePercent = 10m,
                ShortTermTaxExemptMonthlyIncome = 5_000_000m,
                LegalReference = "NĐ 253/2026/NĐ-CP",
            },
        };

        var inputs = await CollectAsync(db, compliance);

        inputs.Numbers[PayrollFieldKeys.ShortTermTaxExemptIncome].Should().Be(5_000_000m);
        inputs.Numbers[PayrollFieldKeys.ShortTermFlatRate].Should().Be(0.10m);
    }
}
