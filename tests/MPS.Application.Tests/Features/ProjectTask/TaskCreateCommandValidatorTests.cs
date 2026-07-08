using FluentAssertions;
using MPS.Application.Features.ProjectTask.Commands;
using MPS.Application.Features.ProjectTask.Validators;
using MPS.Domain.Enums;
using Xunit;

namespace MPS.Application.Tests.Features.ProjectTask;

public sealed class TaskCreateCommandValidatorTests
{
    private static TaskCreateCommand StageCommand(IReadOnlyList<int> assignees) =>
        new(
            ProjectId: 1, DepartmentId: 3, ParentTaskId: null,
            Title: "Cat khung", Description: null,
            ProgressType: ProgressType.Quantity,
            PlannedQuantity: 500, PlannedWorkUnits: 500, IsRequiredGate: false,
            Deadline: null, Priority: null, StepOrder: 1,
            AssigneeUserIds: assignees,
            IsBatchParent: true);

    private static TaskCreateCommand QuantityLeafCommand(IReadOnlyList<int> assignees) =>
        new(
            ProjectId: 1, DepartmentId: 3, ParentTaskId: 5,
            Title: "Cat khung - NV", Description: null,
            ProgressType: ProgressType.Quantity,
            PlannedQuantity: 100, PlannedWorkUnits: 0, IsRequiredGate: false,
            Deadline: null, Priority: null, StepOrder: 1,
            AssigneeUserIds: assignees,
            IsBatchParent: false);

    private static TaskCreateCommand GateCommand(IReadOnlyList<int> assignees) =>
        new(
            ProjectId: 1, DepartmentId: 3, ParentTaskId: null,
            Title: "QC khach duyet", Description: null,
            ProgressType: ProgressType.Milestone,
            PlannedQuantity: 0, PlannedWorkUnits: 0, IsRequiredGate: true,
            Deadline: null, Priority: null, StepOrder: 2,
            AssigneeUserIds: assignees,
            IsBatchParent: false);

    [Fact]
    public void Validate_Passes_WhenStageHasEmptyAssignees()
    {
        var result = new TaskCreateCommandValidator().Validate(StageCommand(Array.Empty<int>()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_WhenStageHasAssignees()
    {
        var result = new TaskCreateCommandValidator().Validate(StageCommand(new[] { 11 }));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AssigneeUserIds");
    }

    [Fact]
    public void Validate_Fails_WhenQuantityLeafHasNoAssignees()
    {
        var result = new TaskCreateCommandValidator().Validate(QuantityLeafCommand(Array.Empty<int>()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AssigneeUserIds");
    }

    [Fact]
    public void Validate_Passes_WhenQuantityLeafHasAssignees()
    {
        var result = new TaskCreateCommandValidator().Validate(QuantityLeafCommand(new[] { 11 }));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Passes_WhenGateHasNoAssignees()
    {
        var result = new TaskCreateCommandValidator().Validate(GateCommand(Array.Empty<int>()));

        result.IsValid.Should().BeTrue();
    }
}
