using FluentAssertions;
using Moq;
using MPS.Application.Features.ProcessTemplate.Commands;
using MPS.Application.Features.ProcessTemplate.Validators;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.ProcessTemplate;

// Refactor: validator giữ luật cấp đơn (Code/Name/BaseQuantity/Steps); bất biến cây N tầng validate trong
// ProcessTemplateTreeBuilder (container/leaf/gate, headcount/position, ratio nesting).
public sealed class ProcessTemplateCreateCommandValidatorTests
{
    private readonly Mock<IProcessTemplateRepository> _repository = new();

    private ProcessTemplateCreateCommandValidator CreateSut()
    {
        _repository.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return new ProcessTemplateCreateCommandValidator(_repository.Object);
    }

    private static ProcessStepInput Leaf(string name = "Thợ cắt", int order = 1, int headcount = 3, int? positionId = 42) =>
        new(name, null, order, ProgressType.Quantity, DefaultPlannedQuantity: 100, DefaultPlannedWorkUnits: 100,
            DefaultIsRequiredGate: false, PlannedHeadcount: headcount, RequiredPositionId: positionId,
            ScalesWithQuantity: true, DefaultDeadlineOffsetRatio: null, Priority: 2, Children: Array.Empty<ProcessStepInput>());

    private static ProcessStepInput Gate(IReadOnlyList<ProcessStepInput>? children = null) =>
        new("QC", null, 2, ProgressType.Milestone, 0, 0, DefaultIsRequiredGate: true,
            PlannedHeadcount: 0, RequiredPositionId: null, ScalesWithQuantity: false,
            DefaultDeadlineOffsetRatio: null, Priority: 2, Children: children ?? Array.Empty<ProcessStepInput>());

    private static ProcessTemplateCreateCommand Command(int baseQuantity, params ProcessStepInput[] steps) =>
        new("KHUNG-A4", "Gia công khung tranh A4", null, baseQuantity, null, steps);

    [Fact]
    public async Task Validate_Passes_ForValidCommand()
    {
        var result = await CreateSut().ValidateAsync(Command(100, Leaf()));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_Fails_WhenBaseQuantityNotPositive()
    {
        var result = await CreateSut().ValidateAsync(Command(0, Leaf()));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_Fails_WhenNoSteps()
    {
        var result = await CreateSut().ValidateAsync(Command(100));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void TreeBuilder_Throws_WhenLeafHasNoPosition()
    {
        var act = () => ProcessTemplateTreeBuilder.Build(new[] { Leaf(positionId: null) });
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void TreeBuilder_Throws_WhenLeafHeadcountBelowOne()
    {
        var act = () => ProcessTemplateTreeBuilder.Build(new[] { Leaf(headcount: 0) });
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void TreeBuilder_Throws_WhenGateHasChildren()
    {
        var act = () => ProcessTemplateTreeBuilder.Build(new[] { Gate(new[] { Leaf() }) });
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void TreeBuilder_Throws_WhenChildRatioExceedsParent()
    {
        var parent = new ProcessStepInput(
            "Cắt", null, 1, ProgressType.Quantity, 0, 500, false, 0, null, false,
            DefaultDeadlineOffsetRatio: 0.4, Priority: 2,
            Children: new[]
            {
                Leaf() with { DefaultDeadlineOffsetRatio = 0.9 },
            });

        var act = () => ProcessTemplateTreeBuilder.Build(new[] { parent });
        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void TreeBuilder_BuildsContainerWithLeafAndGate_Flat()
    {
        var container = new ProcessStepInput(
            "Cắt", null, 1, ProgressType.Quantity, 0, 500, false, 0, null, false, null, 2,
            Children: new[] { Leaf(), Gate() });

        var flat = ProcessTemplateTreeBuilder.Build(new[] { container });

        flat.Should().HaveCount(3);
        flat.Single(s => s.Name == "Cắt").Children.Should().HaveCount(2);
    }
}
