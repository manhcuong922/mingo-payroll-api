using FluentValidation.TestHelper;
using Moq;
using MPS.Application.Features.SalaryGrade.Commands;
using MPS.Application.Features.SalaryGrade.Validators;
using MPS.Domain.Interfaces;

namespace MPS.Application.Tests.Features.SalaryGrade;

/// <summary>
/// Unit test cho validator tạo SalaryGrade — cover các rule sau khi Code do BE tự sinh.
/// </summary>
public sealed class SalaryGradeCreateCommandValidatorTests
{
    [Fact]
    public async Task PositionId_Must_Be_Positive()
    {
        var validator = NewValidator(positionExists: true);
        var result = await validator.TestValidateAsync(ValidCmd() with { PositionId = 0 });

        result.ShouldHaveValidationErrorFor(c => c.PositionId)
            .WithErrorMessage("PositionId is required.");
    }

    [Fact]
    public async Task Missing_Position_Is_Rejected()
    {
        var validator = NewValidator(positionExists: false);
        var result = await validator.TestValidateAsync(ValidCmd());

        result.ShouldHaveValidationErrorFor(c => c.PositionId)
            .WithErrorMessage("Position does not exist.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Level_Must_Be_Positive(int level)
    {
        var validator = NewValidator(positionExists: true);
        var result = await validator.TestValidateAsync(ValidCmd() with { Level = level });

        result.ShouldHaveValidationErrorFor(c => c.Level)
            .WithErrorMessage("Level must be greater than 0.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public async Task BaseSalary_Must_Be_Positive(decimal salary)
    {
        var validator = NewValidator(positionExists: true);
        var result = await validator.TestValidateAsync(ValidCmd() with { BaseSalary = salary });

        result.ShouldHaveValidationErrorFor(c => c.BaseSalary)
            .WithErrorMessage("BaseSalary must be greater than 0.");
    }

    [Theory]
    [InlineData("VN")]    // chỉ 2 chữ
    [InlineData("VNDX")]  // 4 chữ
    [InlineData("12A")]   // có digit
    public async Task Currency_Must_Be_3_Letter_ISO4217(string currency)
    {
        var validator = NewValidator(positionExists: true);
        var result = await validator.TestValidateAsync(ValidCmd() with { Currency = currency });

        result.ShouldHaveValidationErrorFor(c => c.Currency);
    }

    [Fact]
    public async Task Valid_Command_Passes_All_Rules()
    {
        var validator = NewValidator(positionExists: true);
        var result = await validator.TestValidateAsync(ValidCmd());

        result.ShouldNotHaveAnyValidationErrors();
    }

    private static SalaryGradeCreateCommandValidator NewValidator(bool positionExists)
    {
        var repo = new Mock<IPositionRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(positionExists
                ? new MPS.Domain.Entities.Position { Id = 1, Code = "SE", Name = "Senior Engineer" }
                : null);
        return new SalaryGradeCreateCommandValidator(repo.Object);
    }

    private static SalaryGradeCreateCommand ValidCmd() => new(
        Name: "Senior Engineer Bậc 3",
        PositionId: 1,
        Level: 3,
        BaseSalary: 25_000_000m,
        Currency: "VND",
        Description: null);
}
