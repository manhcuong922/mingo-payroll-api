using FluentAssertions;
using MPS.Application.Features.ProjectTask.Commands;
using MPS.Application.Features.ProjectTask.Validators;
using MPS.Domain.Enums;
using Xunit;

namespace MPS.Application.Tests.Features.ProjectTask;

// Bug đã fix: validator từng bắt AssigneeUserIds.NotEmpty() cho MỌI request, kể cả IsBatchParent=true,
// trong khi TaskCreateCommandHandler.ValidateAssignment lại yêu cầu AssigneeUserIds RỖNG cho công đoạn —
// khiến không cách nào tạo công đoạn thủ công thành công (validator luôn reject trước khi tới handler).
public sealed class TaskCreateCommandValidatorTests
{
    private static TaskCreateCommand StageCommand(IReadOnlyList<int> assignees) =>
        new(
            ProjectId: 1, DepartmentId: 3, ParentTaskId: null,
            Title: "Cắt khung", Description: null,
            ProgressType: ProgressType.Quantity,
            PlannedQuantity: 500, PlannedWorkUnits: 500, IsRequiredGate: false,
            Deadline: null, Priority: null, StepOrder: 1,
            AssigneeUserIds: assignees,
            IsBatchParent: true);

    private static TaskCreateCommand LeafCommand(IReadOnlyList<int> assignees) =>
        new(
            ProjectId: 1, DepartmentId: 3, ParentTaskId: 5,
            Title: "Cắt khung - NV", Description: null,
            ProgressType: ProgressType.Quantity,
            PlannedQuantity: 100, PlannedWorkUnits: 0, IsRequiredGate: false,
            Deadline: null, Priority: null, StepOrder: 1,
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
    public void Validate_Fails_WhenLeafHasNoAssignees()
    {
        var result = new TaskCreateCommandValidator().Validate(LeafCommand(Array.Empty<int>()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AssigneeUserIds");
    }

    [Fact]
    public void Validate_Passes_WhenLeafHasAssignees()
    {
        var result = new TaskCreateCommandValidator().Validate(LeafCommand(new[] { 11 }));

        result.IsValid.Should().BeTrue();
    }
}
