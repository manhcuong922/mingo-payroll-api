using FluentAssertions;
using MPS.Application.Features.ContractAppendix.Common;
using MPS.Domain.Exceptions;

namespace MPS.Application.Tests.Features.ContractAppendix.Common;

/// <summary>
/// Test schema validator của ChangedClausesJson — guard rule Điều 22 k.2 (cấm sửa thời hạn + loại HĐ)
/// + happy path parse → AppendixChangeSet.
/// </summary>
public sealed class AppendixSchemaValidatorTests
{
    /// <summary>JSON cú pháp sai → throw.</summary>
    [Fact]
    public void Throws_When_Json_Is_Malformed()
    {
        var act = () => AppendixSchemaValidator.ParseAndValidate("{invalid json");
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*not valid JSON*");
    }

    /// <summary>JSON là array thay vì object → throw.</summary>
    [Fact]
    public void Throws_When_Root_Is_Array()
    {
        var act = () => AppendixSchemaValidator.ParseAndValidate("[1,2,3]");
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*must be a JSON object*");
    }

    /// <summary>JSON rỗng / chỉ có khoảng trắng → throw.</summary>
    [Fact]
    public void Throws_When_Json_Is_Empty()
    {
        var act = () => AppendixSchemaValidator.ParseAndValidate("   ");
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*required*");
    }

    /// <summary>
    /// Cấm key "EffectiveFrom" — Điều 22 k.2: phụ lục KHÔNG được sửa thời hạn HĐ.
    /// </summary>
    [Fact]
    public void Throws_When_Forbidden_Key_EffectiveFrom_Present()
    {
        var json = """{"EffectiveFrom": "2027-01-01", "JobDescription": "x"}""";
        var act = () => AppendixSchemaValidator.ParseAndValidate(json);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*FORBIDDEN_APPENDIX_KEY*EffectiveFrom*");
    }

    /// <summary>Cấm "ContractCategory" — không thể đổi Định hạn → Vô thời hạn qua phụ lục.</summary>
    [Fact]
    public void Throws_When_Forbidden_Key_ContractCategory_Present()
    {
        var json = """{"ContractCategory": "Indefinite", "JobDescription": "x"}""";
        var act = () => AppendixSchemaValidator.ParseAndValidate(json);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*FORBIDDEN_APPENDIX_KEY*ContractCategory*");
    }

    /// <summary>Cấm "Kind" — không thể đổi loại HĐ (HĐLĐ → HĐ dịch vụ...) qua phụ lục.</summary>
    [Fact]
    public void Throws_When_Forbidden_Key_Kind_Present()
    {
        var json = """{"Kind": "Service", "JobDescription": "x"}""";
        var act = () => AppendixSchemaValidator.ParseAndValidate(json);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*FORBIDDEN_APPENDIX_KEY*Kind*");
    }

    /// <summary>Tên key cấm có thể đến dưới dạng camelCase — vẫn phải chặn (case-insensitive).</summary>
    [Fact]
    public void Throws_When_Forbidden_Key_CamelCase()
    {
        var json = """{"effectiveTo": "2027-01-01", "jobDescription": "x"}""";
        var act = () => AppendixSchemaValidator.ParseAndValidate(json);
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*FORBIDDEN_APPENDIX_KEY*");
    }

    /// <summary>JSON object hợp lệ nhưng không có field actionable → throw.</summary>
    [Fact]
    public void Throws_When_No_Effective_Change()
    {
        var act = () => AppendixSchemaValidator.ParseAndValidate("{}");
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*does not contain any actionable change*");
    }

    /// <summary>Happy path: JSON với JobDescription + SalaryGradeId + AddAllowances → parse OK.</summary>
    [Fact]
    public void Parses_Valid_Change_Set_With_All_Field_Types()
    {
        var json = """
        {
          "jobDescription": "Tech Lead",
          "workLocation": "TP.HCM",
          "salaryGradeId": 5,
          "addAllowances": [
            {"allowanceTypeId": 10, "amount": 1000000, "currency": "VND"}
          ],
          "updateAllowances": [
            {"allowanceTypeId": 11, "amount": 2000000}
          ],
          "removeAllowanceTypeIds": [12, 13]
        }
        """;

        var change = AppendixSchemaValidator.ParseAndValidate(json);

        change.JobDescription.Should().Be("Tech Lead");
        change.WorkLocation.Should().Be("TP.HCM");
        change.SalaryGradeId.Should().Be(5);
        change.AddAllowances.Should().HaveCount(1);
        change.AddAllowances[0].AllowanceTypeId.Should().Be(10);
        change.AddAllowances[0].Amount.Should().Be(1_000_000m);
        change.AddAllowances[0].Currency.Should().Be("VND");
        change.UpdateAllowances.Should().HaveCount(1);
        change.UpdateAllowances[0].AllowanceTypeId.Should().Be(11);
        change.RemoveAllowanceTypeIds.Should().BeEquivalentTo(new[] { 12, 13 });
    }
}
