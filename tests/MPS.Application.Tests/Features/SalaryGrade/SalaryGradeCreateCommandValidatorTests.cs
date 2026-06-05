using FluentValidation.TestHelper;
using Moq;
using MPS.Application.Features.SalaryGrade.Commands;
using MPS.Application.Features.SalaryGrade.Validators;
using MPS.Domain.Interfaces;

namespace MPS.Application.Tests.Features.SalaryGrade;

/// <summary>
/// Unit test cho validator tạo SalaryGrade — cover từng rule riêng biệt
/// (Code unique, Currency 3 chữ ISO 4217, BaseSalary > 0, ...).
/// </summary>
public sealed class SalaryGradeCreateCommandValidatorTests
{
    /// <summary>Code có ký tự đặc biệt → fail.</summary>
    [Fact]
    public async Task Code_With_Special_Characters_Is_Rejected()
    {
        var validator = NewValidator();
        var cmd = ValidCmd() with { Code = "SE@3" };

        var result = await validator.TestValidateAsync(cmd);
        result.ShouldHaveValidationErrorFor(c => c.Code)
            .WithErrorMessage("Code may only contain letters, digits, hyphen and underscore.");
    }

    /// <summary>Code đã tồn tại → fail (gọi repo.CodeExistsAsync).</summary>
    [Fact]
    public async Task Existing_Code_Is_Rejected()
    {
        var repo = new Mock<ISalaryGradeRepository>();
        repo.Setup(r => r.CodeExistsAsync("SE-3", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var validator = new SalaryGradeCreateCommandValidator(repo.Object);
        var result = await validator.TestValidateAsync(ValidCmd() with { Code = "SE-3" });

        result.ShouldHaveValidationErrorFor(c => c.Code)
            .WithErrorMessage("Code already exists.");
    }

    /// <summary>BaseSalary ≤ 0 → fail.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public async Task BaseSalary_Must_Be_Positive(decimal salary)
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCmd() with { BaseSalary = salary });

        result.ShouldHaveValidationErrorFor(c => c.BaseSalary)
            .WithErrorMessage("BaseSalary must be greater than 0.");
    }

    /// <summary>Currency phải đúng 3 chữ — "VN", "VND-X", "12A" đều fail.</summary>
    [Theory]
    [InlineData("VN")]    // chỉ 2 chữ
    [InlineData("VNDX")]  // 4 chữ
    [InlineData("12A")]   // có digit
    public async Task Currency_Must_Be_3_Letter_ISO4217(string currency)
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCmd() with { Currency = currency });

        result.ShouldHaveValidationErrorFor(c => c.Currency);
    }

    /// <summary>Happy path → no error.</summary>
    [Fact]
    public async Task Valid_Command_Passes_All_Rules()
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCmd());

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Level cung cấp nhưng = 0 → fail (Level phải > 0 khi có).</summary>
    [Fact]
    public async Task Level_When_Provided_Must_Be_Positive()
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCmd() with { Level = 0 });

        result.ShouldHaveValidationErrorFor(c => c.Level)
            .WithErrorMessage("Level must be greater than 0.");
    }

    private static SalaryGradeCreateCommandValidator NewValidator()
    {
        var repo = new Mock<ISalaryGradeRepository>();
        repo.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return new SalaryGradeCreateCommandValidator(repo.Object);
    }

    private static SalaryGradeCreateCommand ValidCmd() => new(
        Code: "SE-3",
        Name: "Senior Engineer Bậc 3",
        PositionId: 1,
        Level: 3,
        BaseSalary: 25_000_000m,
        Currency: "VND",
        Description: null,
        IsActive: true);
}
