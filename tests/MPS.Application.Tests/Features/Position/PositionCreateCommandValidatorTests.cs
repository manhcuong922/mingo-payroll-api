using FluentValidation.TestHelper;
using Moq;
using MPS.Application.Features.Position.Commands;
using MPS.Application.Features.Position.Validators;
using MPS.Domain.Interfaces;

namespace MPS.Application.Tests.Features.Position;

public sealed class PositionCreateCommandValidatorTests
{
    [Fact]
    public async Task Code_Is_Required()
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(new PositionCreateCommand("", "Developer", null));

        result.ShouldHaveValidationErrorFor(c => c.Code)
            .WithErrorMessage("Code is required.");
    }

    [Theory]
    [InlineData("dev@1")]
    [InlineData("dev 1")]
    [InlineData("dev!")]
    public async Task Code_Rejects_Disallowed_Characters(string code)
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(new PositionCreateCommand(code, "Developer", null));

        result.ShouldHaveValidationErrorFor(c => c.Code)
            .WithErrorMessage("Code may only contain letters, digits, hyphen and underscore.");
    }

    [Fact]
    public async Task Duplicate_Code_Is_Rejected()
    {
        var repo = new Mock<IPositionRepository>();
        repo.Setup(r => r.CodeExistsAsync("DEV", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.NameExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var validator = new PositionCreateCommandValidator(repo.Object);
        var result = await validator.TestValidateAsync(new PositionCreateCommand("dev", "Developer", null));

        result.ShouldHaveValidationErrorFor(c => c.Code)
            .WithErrorMessage("Mã chức danh đã tồn tại.");
    }

    [Fact]
    public async Task Valid_Command_Passes()
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(new PositionCreateCommand("DEV", "Developer", null));

        result.ShouldNotHaveAnyValidationErrors();
    }

    private static PositionCreateCommandValidator NewValidator()
    {
        var repo = new Mock<IPositionRepository>();
        repo.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repo.Setup(r => r.NameExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return new PositionCreateCommandValidator(repo.Object);
    }
}
