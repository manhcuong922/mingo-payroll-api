using FluentAssertions;
using MPS.Application.Features.Contract.Validators;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Tests.Features.Contract.Validators;

/// <summary>
/// Tests focused on the new ContractKind behavior added by the "expand contract types"
/// change: rule-driven vs user-choice insurance mode, kind-specific EffectiveTo requirements,
/// and civil-code contract permissiveness.
/// </summary>
public sealed class ContractKindValidationTests
{
    private readonly CreateContractCommandValidator _sut = new();

    // === RuleDriven Kinds: server sets flags; user cannot supply them ===

    [Theory]
    [InlineData(ContractKind.LaborFullTime)]
    [InlineData(ContractKind.Probation)]
    // Phase 1 — HĐ khoán chuyển sang RuleDriven: không BH + thuế khoán 10% là điểm chết,
    // xem PieceWorkContractRulesTests.
    [InlineData(ContractKind.PieceWork)]
    public void Fails_When_RuleDriven_Kind_Supplies_Insurance_Flag(ContractKind kind)
    {
        var cmd = ContractCommandFactory.ValidCreate(kind: kind) with
        {
            SocialInsuranceFlag = true,
        };

        var result = _sut.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("RuleDriven"));
    }

    [Fact]
    public void Fails_When_RuleDriven_Kind_Supplies_TaxPolicy()
    {
        var cmd = ContractCommandFactory.ValidCreate(kind: ContractKind.LaborFullTime) with
        {
            TaxPolicy = TaxPolicy.ProgressivePit,
        };

        var result = _sut.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("RuleDriven"));
    }

    // === UserChoice Kinds: user must supply all 4 fields ===

    [Theory]
    [InlineData(ContractKind.LaborPartTime)]
    [InlineData(ContractKind.Service)]
    [InlineData(ContractKind.Collaborator)]
    [InlineData(ContractKind.VocationalTraining)]
    public void Fails_When_UserChoice_Kind_Missing_Insurance_Flags(ContractKind kind)
    {
        var cmd = ContractCommandFactory.ValidCreate(kind: kind, category: null, subtype: null) with
        {
            SocialInsuranceFlag = null,
            HealthInsuranceFlag = null,
            UnemploymentInsuranceFlag = null,
            TaxPolicy = null,
        };

        var result = _sut.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("yêu cầu cung cấp đủ"));
    }

    [Fact]
    public void Passes_For_Valid_Service_Kind_With_User_Choices()
    {
        var from = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var cmd = ContractCommandFactory.ValidCreate(
            kind: ContractKind.Service,
            category: null,
            subtype: null,
            effectiveFrom: from,
            effectiveTo: from.AddMonths(6));

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeTrue(
            "Service contract with EffectiveTo + user-supplied insurance/tax flags is a valid civil-code contract");
    }

    [Fact]
    public void Passes_For_Valid_Collaborator_Even_With_SI_True()
    {
        // CTV có thể vẫn đóng BHXH nếu HR chọn (edge case: NLĐ đã đóng ở nơi khác — HR override).
        var from = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var cmd = ContractCommandFactory.ValidCreate(
            kind: ContractKind.Collaborator,
            category: null,
            subtype: null,
            effectiveFrom: from,
            effectiveTo: from.AddMonths(3)) with
        {
            SocialInsuranceFlag = true,
            HealthInsuranceFlag = true,
            UnemploymentInsuranceFlag = true,
            TaxPolicy = TaxPolicy.FlatRate10Percent,
        };

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeTrue(
            "System stores user's choice verbatim for UserChoice kinds — no auto-inference.");
    }

    // === EffectiveTo requirement per Kind ===

    [Fact]
    public void Fails_When_Service_Missing_EffectiveTo()
    {
        var cmd = ContractCommandFactory.ValidCreate(
            kind: ContractKind.Service,
            category: null,
            subtype: null) with
        {
            EffectiveTo = null,
        };

        var result = _sut.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("dịch vụ"));
    }

    // === LaborFullTime still needs Category (Definite/Indefinite) ===

    [Fact]
    public void Fails_When_LaborFullTime_Missing_Category()
    {
        var cmd = ContractCommandFactory.ValidCreate(
            kind: ContractKind.LaborFullTime,
            category: null,
            subtype: ContractSubtype.Official);

        var result = _sut.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("LaborFullTime requires ContractCategory"));
    }
}
