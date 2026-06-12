using FluentAssertions;
using Moq;
using MPS.Application.Features.SalaryGrade.Commands;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Tests.Features.SalaryGrade;

public sealed class SalaryGradeCreateCommandHandlerTests
{
    [Fact]
    public async Task Generates_Code_From_Position_Code_And_Level()
    {
        var positionRepo = new Mock<IPositionRepository>();
        positionRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MPS.Domain.Entities.Position { Id = 1, Code = "DEV", Name = "Developer" });

        MPS.Domain.Entities.SalaryGrade? captured = null;
        var gradeRepo = new Mock<ISalaryGradeRepository>();
        gradeRepo.Setup(r => r.CreateWithDeactivationAsync(It.IsAny<MPS.Domain.Entities.SalaryGrade>(), It.IsAny<CancellationToken>()))
            .Callback<MPS.Domain.Entities.SalaryGrade, CancellationToken>((g, _) => captured = g)
            .ReturnsAsync(42);

        var handler = new SalaryGradeCreateCommandHandler(gradeRepo.Object, positionRepo.Object);

        var id = await handler.Handle(new SalaryGradeCreateCommand(
            Name: "Dev Level 1",
            PositionId: 1,
            Level: 1,
            BaseSalary: 10_000_000m,
            Currency: "vnd",
            Description: null), CancellationToken.None);

        id.Should().Be(42);
        captured.Should().NotBeNull();
        captured!.Code.Should().Be("DEV-1");
        captured.IsActive.Should().BeTrue();
        captured.PositionId.Should().Be(1);
        captured.Level.Should().Be(1);
        captured.Currency.Should().Be("VND");
    }

    [Fact]
    public async Task Throws_When_Position_Not_Found()
    {
        var positionRepo = new Mock<IPositionRepository>();
        positionRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MPS.Domain.Entities.Position?)null);
        var gradeRepo = new Mock<ISalaryGradeRepository>();

        var handler = new SalaryGradeCreateCommandHandler(gradeRepo.Object, positionRepo.Object);

        var act = () => handler.Handle(new SalaryGradeCreateCommand(
            Name: "X", PositionId: 99, Level: 1, BaseSalary: 1, Currency: "VND", Description: null),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
        gradeRepo.Verify(r => r.CreateWithDeactivationAsync(It.IsAny<MPS.Domain.Entities.SalaryGrade>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
