using FluentAssertions;
using MPS.Application.Services;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using Xunit;

namespace MPS.Application.Tests.Services;

// Refactor task-full-generation: BuildTree sinh full cây N tầng, scale sản lượng lá (ceil), workunits/headcount
// không scale, gate mọi tầng, ratio → deadline. Container không mang sản lượng trực tiếp.
public sealed class TaskGeneratorTests
{
    private static Project Project(int quantity = 505) => new()
    {
        Id = 1,
        DepartmentId = 3,
        Quantity = quantity,
        StartDate = new DateOnly(2026, 7, 1),
        Deadline = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc),
    };

    // Cây: [1 Cắt (container)] → { [2 Thợ cắt (leaf, scale)], [3 QC (gate)] }; [4 Setup máy (leaf, không scale)].
    private static ProcessTemplate Template(int baseQuantity = 100)
    {
        var t = new ProcessTemplate { Id = 9, DefaultDepartmentId = 3, BaseQuantity = baseQuantity };
        t.Steps.AddRange(new[]
        {
            new ProcessStep { Id = 1, Name = "Cắt", StepOrder = 1, DefaultProgressType = ProgressType.Quantity, DefaultPlannedWorkUnits = 500 },
            new ProcessStep
            {
                Id = 2, ParentProcessStepId = 1, Name = "Thợ cắt", StepOrder = 1,
                DefaultProgressType = ProgressType.Quantity, DefaultPlannedQuantity = 7, DefaultPlannedWorkUnits = 300,
                PlannedHeadcount = 3, RequiredPositionId = 42, ScalesWithQuantity = true, DefaultDeadlineOffsetRatio = 0.5,
            },
            new ProcessStep
            {
                Id = 3, ParentProcessStepId = 1, Name = "QC", StepOrder = 2,
                DefaultProgressType = ProgressType.Milestone, DefaultIsRequiredGate = true,
            },
            new ProcessStep
            {
                Id = 4, Name = "Setup máy", StepOrder = 2,
                DefaultProgressType = ProgressType.Quantity, DefaultPlannedQuantity = 10, DefaultPlannedWorkUnits = 50,
                PlannedHeadcount = 1, RequiredPositionId = 43, ScalesWithQuantity = false,
            },
        });
        return t;
    }

    private static IReadOnlyList<ProjectTask> Build(int quantity = 505, int baseQuantity = 100) =>
        new TaskGenerator().BuildTree(Project(quantity), Template(baseQuantity), createdByUserId: 7);

    [Fact]
    public void BuildTree_ProducesFullTree_AllNodes()
    {
        Build().Should().HaveCount(4);
    }

    [Fact]
    public void BuildTree_Container_HasNoQuantityAndIsBatchParent()
    {
        var container = Build().Single(t => t.Title == "Cắt");
        container.IsBatchParent.Should().BeTrue();
        container.PlannedQuantity.Should().Be(0);
        container.PlannedWorkUnits.Should().Be(500);
    }

    [Fact]
    public void BuildTree_WorkLeaf_ScalesQuantityCeil_KeepsWorkUnitsAndHeadcountAndPosition()
    {
        var leaf = Build().Single(t => t.Title == "Thợ cắt");
        leaf.IsBatchParent.Should().BeFalse();
        leaf.PlannedQuantity.Should().Be(36);           // ceil(7 × 5.05) = ceil(35.35) = 36
        leaf.PlannedWorkUnits.Should().Be(300);          // KHÔNG scale
        leaf.PlannedHeadcount.Should().Be(3);            // KHÔNG scale
        leaf.RequiredPositionId.Should().Be(42);
        leaf.DeadlineOffsetRatio.Should().Be(0.5);
        leaf.Deadline.Should().NotBeNull();              // ratio × timeline
    }

    [Fact]
    public void BuildTree_LeafWithScalesFalse_KeepsQuantityUnchanged()
    {
        Build().Single(t => t.Title == "Setup máy").PlannedQuantity.Should().Be(10);
    }

    [Fact]
    public void BuildTree_Gate_AtAnyTier_IsMilestoneWithZeroQuantity()
    {
        var gate = Build().Single(t => t.Title == "QC");
        gate.ProgressType.Should().Be(ProgressType.Milestone);
        gate.IsRequiredGate.Should().BeTrue();
        gate.PlannedQuantity.Should().Be(0);
    }

    [Fact]
    public void BuildTree_Throws_WhenBaseQuantityNotPositive()
    {
        var act = () => new TaskGenerator().BuildTree(Project(), Template(baseQuantity: 0), createdByUserId: 7);
        act.Should().Throw<DomainValidationException>();
    }
}
