using FluentAssertions;
using MPS.Application.Features.Contract.Common;
using MPS.Application.Features.Contract.Validators;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Tests.Features.Contract;

/// <summary>
/// Phase 1 — hợp đồng KHOÁN VIỆC có chế độ BH/thuế cố định, HR không tick tay được nữa
/// (quyết định PO 18/08/2026).
///
/// <list type="bullet">
///   <item><b>Không đóng bảo hiểm nào</b> — HĐ khoán là hợp đồng dân sự, không thuộc đối tượng
///     BHXH/BHYT/BHTN bắt buộc khi đúng bản chất khoán việc.</item>
///   <item><b>Thuế khoán 10%</b> — TT 111/2013 Đ.25.1.i: khấu trừ 10% với cá nhân cư trú không ký
///     HĐLĐ hoặc ký HĐLĐ dưới 3 tháng.</item>
/// </list>
///
/// <para>Trước đây <c>PieceWork</c> ở chế độ <c>UserChoice</c>: mỗi HĐ khoán phụ thuộc việc HR tick
/// đúng hay sai 4 ô, và không có gì chặn một HĐ khoán bị khai đóng đủ BH + thuế lũy tiến.</para>
/// </summary>
public sealed class PieceWorkContractRulesTests
{
    private readonly CreateContractCommandValidator _validator = new();

    [Fact(DisplayName = "HĐ khoán là RuleDriven — server tự quyết cờ BH + chính sách thuế")]
    public void PieceWork_IsRuleDriven()
    {
        var meta = ContractKindMetadata.Get(ContractKind.PieceWork);

        meta.InsuranceMode.Should().Be(InsuranceDeterminationMode.RuleDriven);
        meta.DefaultSocialInsurance.Should().BeFalse();
        meta.DefaultHealthInsurance.Should().BeFalse();
        meta.DefaultUnemploymentInsurance.Should().BeFalse();
        meta.DefaultTaxPolicy.Should().Be(TaxPolicy.FlatRate10Percent);
    }

    [Theory(DisplayName = "HĐ khoán gửi kèm cờ BH / chính sách thuế → bị từ chối")]
    [InlineData(true, null, null, null)]
    [InlineData(null, true, null, null)]
    [InlineData(null, null, true, null)]
    [InlineData(null, null, null, TaxPolicy.ProgressivePit)]
    public void PieceWork_Rejects_ClientSuppliedInsuranceOrTax(
        bool? social, bool? health, bool? unemployment, TaxPolicy? taxPolicy)
    {
        var cmd = ContractCommandFactory.ValidCreate(
            kind: ContractKind.PieceWork, category: null, subtype: null) with
        {
            SocialInsuranceFlag = social,
            HealthInsuranceFlag = health,
            UnemploymentInsuranceFlag = unemployment,
            TaxPolicy = taxPolicy,
        };

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("RuleDriven"),
            "chế độ BH/thuế của HĐ khoán là điểm chết, không cho client đè");
    }

    [Fact(DisplayName = "HĐ khoán không gửi 4 trường đó → hợp lệ")]
    public void PieceWork_Passes_WhenClientOmitsInsuranceAndTax()
    {
        var cmd = ContractCommandFactory.ValidCreate(
            kind: ContractKind.PieceWork, category: null, subtype: null);

        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e =>
            e.ErrorMessage.Contains("RuleDriven") || e.ErrorMessage.Contains("yêu cầu cung cấp đủ"));
    }

    [Fact(DisplayName = "HĐ khoán dựng ra: 3 cờ BH = false, thuế khoán 10%")]
    public void PieceWork_Entity_HasNoInsuranceAndFlatRateTax()
    {
        var cmd = ContractCommandFactory.ValidCreate(
            kind: ContractKind.PieceWork, category: null, subtype: null);

        var entity = ContractBuilder.FromCreate(
            cmd,
            contractNo: "KV-2026-0001",
            salaryAmountSnapshot: 12_000_000m,
            salaryCurrencySnapshot: LaborLawConstants.Currency.Default,
            createdByUserId: 1,
            nowUtc: new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc));

        entity.SocialInsuranceFlag.Should().BeFalse();
        entity.HealthInsuranceFlag.Should().BeFalse();
        entity.UnemploymentInsuranceFlag.Should().BeFalse();
        entity.TaxPolicy.Should().Be(TaxPolicy.FlatRate10Percent);
    }

    [Fact(DisplayName = "Cờ BH do client gửi bị BỎ QUA kể cả khi lọt qua validator")]
    public void PieceWork_Entity_IgnoresClientSuppliedFlags()
    {
        // Đường vào duy nhất là validator, nhưng builder vẫn phải tự bảo vệ: RuleDriven nghĩa là
        // metadata thắng, không phải "tin client khi validator lỡ cho qua".
        var cmd = ContractCommandFactory.ValidCreate(
            kind: ContractKind.PieceWork, category: null, subtype: null) with
        {
            SocialInsuranceFlag = true,
            HealthInsuranceFlag = true,
            UnemploymentInsuranceFlag = true,
            TaxPolicy = TaxPolicy.ProgressivePit,
        };

        var entity = ContractBuilder.FromCreate(
            cmd,
            contractNo: "KV-2026-0002",
            salaryAmountSnapshot: 12_000_000m,
            salaryCurrencySnapshot: LaborLawConstants.Currency.Default,
            createdByUserId: 1,
            nowUtc: new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc));

        entity.SocialInsuranceFlag.Should().BeFalse();
        entity.TaxPolicy.Should().Be(TaxPolicy.FlatRate10Percent);
    }
}
