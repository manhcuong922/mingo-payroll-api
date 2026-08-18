using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Domain.Enums.Payroll;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// Phase 1 — hợp đồng KHOÁN VIỆC trên bảng lương: KHÔNG đóng bảo hiểm nào, thuế khoán 10%
/// (quyết định PO 18/08/2026).
///
/// <para>Metadata (<c>ContractKindMetadata</c>) đóng băng 3 cờ BH = false + <c>FlatRate10Percent</c>
/// ngay khi tạo hợp đồng; hai test này chốt phần còn lại của chuỗi — collector phải dịch đúng ra cờ
/// mà bảng công thức hiểu.</para>
/// </summary>
public sealed class PayrollCollectorPieceWorkTests
{
    [Fact(DisplayName = "HĐ khoán → cờ BH = 'không tính bảo hiểm'")]
    public async Task PieceWorkContract_ProducesNoInsuranceFlag()
    {
        using var db = await SeedPieceWorkAsync();

        var inputs = await CollectAsync(db);

        inputs.Texts[PayrollFieldKeys.ContractInsuranceFlag].Should().Be(InsuranceFlags.NoInsurance);

        // 3 công tắc "miễn" đều bật ⇒ mọi cột BH (và KPCĐ) của dòng này bị cắt về 0.
        inputs.Numbers[PayrollFieldKeys.ContractNoSocialInsurance].Should().Be(1m);
        inputs.Numbers[PayrollFieldKeys.ContractNoHealthInsurance].Should().Be(1m);
        inputs.Numbers[PayrollFieldKeys.ContractNoUnemploymentInsurance].Should().Be(1m);
    }

    [Fact(DisplayName = "HĐ khoán → cờ thuế khoán 10%")]
    public async Task PieceWorkContract_ProducesFlatRateTaxFlag()
    {
        using var db = await SeedPieceWorkAsync();

        var inputs = await CollectAsync(db);

        inputs.Texts[PayrollFieldKeys.ContractTaxFlag].Should().Be(TaxFlags.Khoan10,
            "TT 111/2013 Đ.25.1.i — khấu trừ 10% với cá nhân cư trú không ký HĐLĐ");
    }

    [Fact(DisplayName = "HĐLĐ vẫn đóng đủ BH + thuế lũy tiến (không nới nhầm)")]
    public async Task LaborContract_KeepsFullInsuranceAndProgressiveTax()
    {
        using var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(day: 10));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Texts[PayrollFieldKeys.ContractInsuranceFlag].Should().Be(InsuranceFlags.Full);
        inputs.Texts[PayrollFieldKeys.ContractTaxFlag].Should().Be(TaxFlags.Progressive);
    }

    /// <summary>
    /// HĐ khoán đúng như <c>ContractKindMetadata</c> dựng ra: 3 cờ BH = false, thuế khoán 10%.
    /// </summary>
    private static async Task<MPS.Infrastructure.Persistence.AppDbContext> SeedPieceWorkAsync()
    {
        var db = await SeedBaseAsync(socialFlag: false, healthFlag: false, unemploymentFlag: false);
        var contract = db.Contracts.Single();
        contract.Kind = ContractEnums.ContractKind.PieceWork;
        contract.TaxPolicy = ContractEnums.TaxPolicy.FlatRate10Percent;
        db.AttendanceRecords.Add(ShiftRecord(day: 10));
        await db.SaveChangesAsync();
        return db;
    }
}
