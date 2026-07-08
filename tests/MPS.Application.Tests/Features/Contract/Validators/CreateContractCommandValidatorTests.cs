using FluentAssertions;
using MPS.Application.Features.Contract.Validators;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Tests.Features.Contract.Validators;

/// <summary>
/// Pure validator tests — exercise the FluentValidation rules in <see cref="ContractValidationRules"/>
/// via <see cref="CreateContractCommandValidator"/>. No DB / mocks needed.
/// Mỗi test cô lập đúng 1 rule bằng cách lấy command happy-path từ factory rồi tweak field cần test.
/// </summary>
public sealed class CreateContractCommandValidatorTests
{
    private readonly CreateContractCommandValidator _sut = new();

    // Case happy: HĐ XĐ thời hạn 12 tháng + chính thức + đầy đủ field → validator pass.
    // Đây là baseline để chắc các test khác fail vì đúng lý do (rule đang test), không vì lỗi khác.
    [Fact]
    public void Passes_For_Valid_Definite_Official_Contract()
    {
        var cmd = ContractCommandFactory.ValidCreate();

        var result = _sut.Validate(cmd);

        result.IsValid.Should().BeTrue(string.Join(" | ", result.Errors.Select(e => e.ErrorMessage)));
    }

    // Case BLLĐ Điều 20: HĐ XĐ thời hạn phải có ngày kết thúc. Để null → reject.
    [Fact]
    public void Fails_When_Definite_Has_No_EffectiveTo()
    {
        var cmd = ContractCommandFactory.ValidCreate() with { EffectiveTo = null };

        var result = _sut.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.ErrorMessage.Contains("Definite") && e.ErrorMessage.Contains("must have EffectiveTo"));
    }

    // Case BLLĐ Điều 20 1b: HĐ XĐ thời hạn ≤ 36 tháng. Set 37 tháng → reject.
    [Fact]
    public void Fails_When_Definite_Exceeds_36_Months()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var cmd = ContractCommandFactory.ValidCreate(
            effectiveFrom: from,
            effectiveTo: from.AddMonths(37));

        var result = _sut.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("36 months"));
    }

    // Case: HĐ KXĐ thời hạn (Indefinite) thì EffectiveTo phải null. Cố tình set ngày kết thúc → reject.
    [Fact]
    public void Fails_When_Indefinite_Has_EffectiveTo()
    {
        var cmd = ContractCommandFactory.ValidCreate(category: ContractCategory.Indefinite) with
        {
            EffectiveTo = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        var result = _sut.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.ErrorMessage.Contains("Indefinite") && e.ErrorMessage.Contains("must NOT have EffectiveTo"));
    }

    // Case BLLĐ Điều 25: Thử việc luôn có thời hạn xác định. Với Kind=Probation, EffectiveTo bắt buộc.
    [Fact]
    public void Fails_When_Probation_Kind_Missing_EffectiveTo()
    {
        var cmd = ContractCommandFactory.ValidCreate(
            kind: ContractKind.Probation,
            category: null,
            subtype: ContractSubtype.Probation) with
        {
            EffectiveTo = null,
        };

        var result = _sut.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.ErrorMessage.Contains("thử việc") && e.ErrorMessage.Contains("EffectiveTo"));
    }

    // Case: HasProbation=true bắt buộc 4 trường ProbationStartDate/EndDate/Salary/ResultStatus.
    // Test cố tình bỏ trống cả 4 → validator báo cả 4 lỗi (không stop ở lỗi đầu).
    [Fact]
    public void Fails_When_Probation_Enabled_Without_Required_Fields()
    {
        var cmd = ContractCommandFactory.ValidCreate(hasProbation: true) with
        {
            ProbationStartDate = null,
            ProbationEndDate = null,
            ProbationSalary = null,
            ProbationResultStatus = null,
        };

        var result = _sut.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("ProbationStartDate is required"));
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("ProbationEndDate is required"));
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("ProbationSalary is required"));
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("ProbationResultStatus is required"));
    }

    // Case BLLĐ Điều 25: max thời gian thử việc = 180 ngày (cho QLDN). Set 200 ngày → reject.
    [Fact]
    public void Fails_When_Probation_Period_Exceeds_180_Days()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var cmd = ContractCommandFactory.ValidCreate(hasProbation: true, effectiveFrom: from) with
        {
            ProbationStartDate = from,
            ProbationEndDate = from.AddDays(200),
        };

        var result = _sut.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Probation period"));
    }

    // Case BLLĐ Điều 24 k.3: Không áp dụng thử việc cho HĐ <30 ngày.
    // HĐ 20 ngày + HasProbation=true → reject.
    [Fact]
    public void Fails_When_HasProbation_But_Contract_Shorter_Than_30_Days()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var cmd = ContractCommandFactory.ValidCreate(
            hasProbation: true,
            effectiveFrom: from,
            effectiveTo: from.AddDays(20));

        var result = _sut.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Probation is not allowed"));
    }

    // Case schema: gửi 2 dòng cùng AllowanceTypeId (5) — không có nghĩa nghiệp vụ, reject.
    // Junction ContractAllowances cũng có UNIQUE(ContractId, AllowanceTypeId) bảo vệ ở DB.
    [Fact]
    public void Fails_When_Duplicate_AllowanceTypeId_In_Payload()
    {
        var cmd = ContractCommandFactory.ValidCreate(
            allowances: new[]
            {
                new MPS.Application.Features.Contract.Commands.ContractAllowanceInput(5, 500_000m, null, null),
                new MPS.Application.Features.Contract.Commands.ContractAllowanceInput(5, 600_000m, null, null),
            });

        var result = _sut.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Duplicate AllowanceTypeId"));
    }

    // Case: Currency của allowance phải 3 ký tự ISO 4217. "VN" sai → reject.
    [Fact]
    public void Fails_When_Allowance_Currency_Is_Not_3_Letters()
    {
        var cmd = ContractCommandFactory.ValidCreate(
            allowances: new[]
            {
                new MPS.Application.Features.Contract.Commands.ContractAllowanceInput(5, 500_000m, "VN", null),
            });

        var result = _sut.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Currency must be a 3-letter ISO 4217 code"));
    }

    // Case logic ngày: EffectiveFrom == EffectiveTo → reject (HĐ phải có thời lượng > 0).
    [Fact]
    public void Fails_When_EffectiveFrom_Greater_Or_Equal_To_EffectiveTo()
    {
        var from = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var cmd = ContractCommandFactory.ValidCreate(
            effectiveFrom: from,
            effectiveTo: from);

        var result = _sut.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("EffectiveFrom must be earlier than EffectiveTo"));
    }
}
