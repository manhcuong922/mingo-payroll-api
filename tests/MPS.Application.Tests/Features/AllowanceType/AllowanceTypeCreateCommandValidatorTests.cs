using FluentValidation.TestHelper;
using Moq;
using MPS.Application.Features.AllowanceType.Commands;
using MPS.Application.Features.AllowanceType.Validators;
using MPS.Domain.Interfaces;

namespace MPS.Application.Tests.Features.AllowanceType;

/// <summary>
/// Unit test cho validator tạo AllowanceType — cover từng rule riêng biệt.
/// </summary>
public sealed class AllowanceTypeCreateCommandValidatorTests
{
    /// <summary>Code rỗng → fail.</summary>
    [Fact]
    public async Task Code_Required()
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCmd() with { Code = string.Empty });

        result.ShouldHaveValidationErrorFor(c => c.Code)
            .WithErrorMessage("Code is required.");
    }

    /// <summary>Code chứa khoảng trắng / dấu chấm → fail.</summary>
    [Theory]
    [InlineData("LUNCH ALLOWANCE")]
    [InlineData("LUNCH.A")]
    [InlineData("LUNCH/A")]
    public async Task Code_Pattern_Rejects_Invalid_Chars(string code)
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCmd() with { Code = code });

        result.ShouldHaveValidationErrorFor(c => c.Code)
            .WithErrorMessage("Code may only contain letters, digits, hyphen and underscore.");
    }

    /// <summary>Code đã tồn tại → fail (gọi repo.CodeExistsAsync).</summary>
    [Fact]
    public async Task Existing_Code_Is_Rejected()
    {
        var repo = new Mock<IAllowanceTypeRepository>();
        repo.Setup(r => r.CodeExistsAsync("LUNCH", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var validator = new AllowanceTypeCreateCommandValidator(repo.Object);
        var result = await validator.TestValidateAsync(ValidCmd() with { Code = "LUNCH" });

        result.ShouldHaveValidationErrorFor(c => c.Code)
            .WithErrorMessage("Code already exists.");
    }

    /// <summary>DefaultAmount âm → fail.</summary>
    [Fact]
    public async Task DefaultAmount_When_Provided_Must_Be_NonNegative()
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCmd() with { DefaultAmount = -1 });

        result.ShouldHaveValidationErrorFor(c => c.DefaultAmount)
            .WithErrorMessage("DefaultAmount must be greater than or equal to 0.");
    }

    /// <summary>DefaultAmount = 0 → OK (≥ 0).</summary>
    [Fact]
    public async Task DefaultAmount_Zero_Is_Allowed()
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCmd() with { DefaultAmount = 0 });

        result.ShouldNotHaveValidationErrorFor(c => c.DefaultAmount);
    }

    /// <summary>Happy path → no error.</summary>
    [Fact]
    public async Task Valid_Command_Passes_All_Rules()
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCmd());

        result.ShouldNotHaveAnyValidationErrors();
    }

    private static AllowanceTypeCreateCommandValidator NewValidator()
    {
        var repo = new Mock<IAllowanceTypeRepository>();
        repo.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return new AllowanceTypeCreateCommandValidator(repo.Object);
    }

    private static AllowanceTypeCreateCommand ValidCmd() => new(
        Code: "LUNCH",
        Name: "Phụ cấp ăn trưa",
        Description: null,
        DefaultAmount: 730_000m,
        IsBhxhBase: false,
        IsTaxable: false,
        IsActive: true);
}
