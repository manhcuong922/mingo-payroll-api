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
        // Trọng số hợp lệ (gốc 100, con 100) để lỗi nổi lên là do ratio con > cha, không phải tổng nhóm.
        var parent = new ProcessStepInput(
            "Cắt", null, 1, ProgressType.Quantity, 0, 100, false, 0, null, false,
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
        // Gốc: 1 container = 100. Con: 1 leaf = 100 (+ gate không tính) ⇒ mọi nhóm cộng đúng 100.
        var container = new ProcessStepInput(
            "Cắt", null, 1, ProgressType.Quantity, 0, 100, false, 0, null, false, null, 2,
            Children: new[] { Leaf(), Gate() });

        var flat = ProcessTemplateTreeBuilder.Build(new[] { container });

        flat.Should().HaveCount(3);
        flat.Single(s => s.Name == "Cắt").Children.Should().HaveCount(2);
    }

    [Fact]
    public void TreeBuilder_Throws_WhenContainerHasZeroWorkUnits()
    {
        // Trọng số nhóm phải > 0 — bắt ngay khi soạn template thay vì để lỗi runtime lúc generate.
        var container = new ProcessStepInput(
            "Cắt", null, 1, ProgressType.Quantity, 0, DefaultPlannedWorkUnits: 0, false, 0, null, false, null, 2,
            Children: new[] { Leaf() });

        var act = () => ProcessTemplateTreeBuilder.Build(new[] { container });
        act.Should().Throw<DomainValidationException>().WithMessage("*trọng số*");
    }

    [Fact]
    public void TreeBuilder_Throws_WhenTopLevelWeightsDoNotSumTo100()
    {
        // 2 node gốc 60 + 30 = 90 ≠ 100 ⇒ chặn: khi mọi việc xong đơn không đạt 100%.
        var a = Leaf(name: "A", order: 1) with { DefaultPlannedWorkUnits = 60 };
        var b = Leaf(name: "B", order: 2) with { DefaultPlannedWorkUnits = 30 };

        var act = () => ProcessTemplateTreeBuilder.Build(new[] { a, b });
        act.Should().Throw<DomainValidationException>().WithMessage("*= 100*");
    }

    [Fact]
    public void TreeBuilder_Throws_WhenChildrenWeightsDoNotSumTo100()
    {
        // Nhóm gốc = 100 (hợp lệ) nhưng con 70 + 20 = 90 ≠ 100 ⇒ chặn ở tầng con.
        var container = new ProcessStepInput(
            "Cắt", null, 1, ProgressType.Quantity, 0, 100, false, 0, null, false, null, 2,
            Children: new[]
            {
                Leaf(name: "A", order: 1) with { DefaultPlannedWorkUnits = 70 },
                Leaf(name: "B", order: 2) with { DefaultPlannedWorkUnits = 20 },
            });

        var act = () => ProcessTemplateTreeBuilder.Build(new[] { container });
        act.Should().Throw<DomainValidationException>().WithMessage("*= 100*");
    }

    [Fact]
    public void TreeBuilder_Passes_WhenGroupWeightsSumTo100_IgnoringGate()
    {
        // Gốc 100; con 60 + 40 = 100; Gate không tính vào tổng ⇒ hợp lệ.
        var container = new ProcessStepInput(
            "Cắt", null, 1, ProgressType.Quantity, 0, 100, false, 0, null, false, null, 2,
            Children: new[]
            {
                Leaf(name: "A", order: 1) with { DefaultPlannedWorkUnits = 60 },
                Leaf(name: "B", order: 2) with { DefaultPlannedWorkUnits = 40 },
                Gate() with { StepOrder = 3 },
            });

        var flat = ProcessTemplateTreeBuilder.Build(new[] { container });

        flat.Should().HaveCount(4);
    }

    [Fact]
    public void TreeBuilder_Throws_WhenLeafHasZeroWorkUnits()
    {
        var leaf = Leaf() with { DefaultPlannedWorkUnits = 0 };

        var act = () => ProcessTemplateTreeBuilder.Build(new[] { leaf });
        act.Should().Throw<DomainValidationException>().WithMessage("*trọng số*");
    }

    [Fact]
    public void TreeBuilder_ForcesGateWorkUnits_ToZero_EvenIfInputPositive()
    {
        // User có thể lỡ nhập WorkUnits > 0 cho Gate; builder phải khoá lại về 0 để rollup
        // không bao giờ đếm Gate vào % (Gate cũng không tính vào tổng-100 của nhóm).
        var gateWithBogusWorkUnits = new ProcessStepInput(
            "QC", null, 2, ProgressType.Milestone, 0, DefaultPlannedWorkUnits: 999,
            DefaultIsRequiredGate: true, 0, null, false, null, 2, Array.Empty<ProcessStepInput>());

        // Leaf 100 để nhóm gốc (bỏ Gate) cộng đúng 100 → template hợp lệ.
        var flat = ProcessTemplateTreeBuilder.Build(new[] { Leaf(order: 1), gateWithBogusWorkUnits });

        flat.Single(s => s.Name == "QC").DefaultPlannedWorkUnits.Should().Be(0);
    }
}
