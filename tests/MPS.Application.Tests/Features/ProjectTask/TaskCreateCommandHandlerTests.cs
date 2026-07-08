using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.ProjectTask.Commands;
using MPS.Application.Services;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.ProjectTask;

public sealed class TaskCreateCommandHandlerTests
{
    private readonly Mock<IProjectTaskRepository> _tasks = new();
    private readonly Mock<IProjectRepository> _projects = new();
    private readonly Mock<IDepartmentRepository> _departments = new();
    private readonly Mock<IDepartmentManagerAuthorizer> _authorizer = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private const int ManagerUserId = 7;
    private const int DeptId = 3;

    private TaskCreateCommandHandler CreateSut() =>
        new(_tasks.Object, _projects.Object, _departments.Object, _authorizer.Object, _currentUser.Object);

    private const int StageId = 5;

    // Task lá (IsBatchParent=false) chỉ tạo được bởi đúng PM phụ trách công đoạn cha (v7 mục 8) —
    // set ManagerUserId làm AssignedPmUserId của công đoạn #5 để các test tạo task lá qua được guard.
    private void SetupManagerWithMembers(params int[] memberUserIds)
    {
        _currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        _authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _departments.Setup(d => d.GetMemberUserIdsAsync(DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberUserIds);
        _tasks.Setup(t => t.GetByIdAsync(StageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MPS.Domain.Entities.ProjectTask
            {
                Id = StageId, IsBatchParent = true, DepartmentId = DeptId, AssignedPmUserId = ManagerUserId,
                ProgressType = ProgressType.Quantity, PlannedQuantity = 1000,
            });
        _tasks.Setup(t => t.AddAsync(It.IsAny<MPS.Domain.Entities.ProjectTask>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
    }

    private static TaskCreateCommand Command(IReadOnlyList<int> assignees) =>
        new(
            ProjectId: 1, DepartmentId: DeptId, ParentTaskId: StageId,
            Title: "Cắt khung", Description: null,
            ProgressType: ProgressType.Quantity,
            PlannedQuantity: 100, PlannedWorkUnits: 0, IsRequiredGate: false,
            Deadline: null, Priority: null, StepOrder: 1,
            AssigneeUserIds: assignees);

    [Fact]
    public async Task Create_SingleAssignee_CreatesTaskWithOneAssignee()
    {
        SetupManagerWithMembers(11, 12, 13);
        MPS.Domain.Entities.ProjectTask? captured = null;
        _tasks.Setup(t => t.AddAsync(It.IsAny<MPS.Domain.Entities.ProjectTask>(), It.IsAny<CancellationToken>()))
            .Callback<MPS.Domain.Entities.ProjectTask, CancellationToken>((t, _) => captured = t)
            .ReturnsAsync(42);

        var id = await CreateSut().Handle(Command(new[] { 11 }), CancellationToken.None);

        id.Should().Be(42);
        captured!.Assignees.Should().HaveCount(1);
        captured.Assignees.Single().UserId.Should().Be(11);
    }

    [Fact]
    public async Task Create_GroupAssignees_CreatesTaskWithManyAssignees()
    {
        SetupManagerWithMembers(11, 12, 13, 14, 15);
        MPS.Domain.Entities.ProjectTask? captured = null;
        _tasks.Setup(t => t.AddAsync(It.IsAny<MPS.Domain.Entities.ProjectTask>(), It.IsAny<CancellationToken>()))
            .Callback<MPS.Domain.Entities.ProjectTask, CancellationToken>((t, _) => captured = t)
            .ReturnsAsync(42);

        await CreateSut().Handle(Command(new[] { 11, 12, 13, 14, 15 }), CancellationToken.None);

        captured!.Assignees.Select(a => a.UserId).Should().BeEquivalentTo(new[] { 11, 12, 13, 14, 15 });
    }

    [Fact]
    public async Task Create_Throws_WhenAssigneeNotInDepartment()
    {
        SetupManagerWithMembers(11, 12);   // 99 không thuộc xưởng

        var act = () => CreateSut().Handle(Command(new[] { 11, 99 }), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
        _tasks.Verify(t => t.AddAsync(It.IsAny<MPS.Domain.Entities.ProjectTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_AllowsGateChild_WithoutConsumingParentQuantityBudget()
    {
        // Công đoạn #5 đo theo Quantity, PlannedQuantity=1000, sibling con Quantity đã chiếm 960
        // (SumChildrenPlannedQuantityAsync mock) — Gate (Milestone) không có sản lượng nên KHÔNG bị
        // chặn bởi ngân sách PlannedQuantity còn lại, dù chỉ còn dư 40.
        SetupManagerWithMembers(11, 12, 13);
        _tasks.Setup(t => t.SumChildrenPlannedQuantityAsync(StageId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(960);
        MPS.Domain.Entities.ProjectTask? captured = null;
        _tasks.Setup(t => t.AddAsync(It.IsAny<MPS.Domain.Entities.ProjectTask>(), It.IsAny<CancellationToken>()))
            .Callback<MPS.Domain.Entities.ProjectTask, CancellationToken>((t, _) => captured = t)
            .ReturnsAsync(42);
        var gateCommand = new TaskCreateCommand(
            ProjectId: 1, DepartmentId: DeptId, ParentTaskId: StageId,
            Title: "QC mẫu sơn", Description: null,
            ProgressType: ProgressType.Milestone,
            PlannedQuantity: 0, PlannedWorkUnits: 0, IsRequiredGate: true,
            Deadline: null, Priority: null, StepOrder: 1,
            AssigneeUserIds: new[] { 11 });

        var id = await CreateSut().Handle(gateCommand, CancellationToken.None);

        id.Should().Be(42);
        captured!.ProgressType.Should().Be(ProgressType.Milestone);
        captured.PlannedQuantity.Should().Be(0);
        captured.IsRequiredGate.Should().BeTrue();
    }

    [Fact]
    public async Task Create_AllowsProjectLevelGate_WithoutAssignees()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        _authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        MPS.Domain.Entities.ProjectTask? captured = null;
        _tasks.Setup(t => t.AddAsync(It.IsAny<MPS.Domain.Entities.ProjectTask>(), It.IsAny<CancellationToken>()))
            .Callback<MPS.Domain.Entities.ProjectTask, CancellationToken>((t, _) => captured = t)
            .ReturnsAsync(77);
        var command = new TaskCreateCommand(
            ProjectId: 1, DepartmentId: DeptId, ParentTaskId: null,
            Title: "QC khach duyet", Description: null,
            ProgressType: ProgressType.Milestone,
            PlannedQuantity: 0, PlannedWorkUnits: 0, IsRequiredGate: true,
            Deadline: null, Priority: null, StepOrder: 5,
            AssigneeUserIds: Array.Empty<int>());

        var id = await CreateSut().Handle(command, CancellationToken.None);

        id.Should().Be(77);
        captured!.ProjectId.Should().Be(1);
        captured.ParentTaskId.Should().BeNull();
        captured.ProgressType.Should().Be(ProgressType.Milestone);
        captured.IsBatchParent.Should().BeFalse();
        captured.PlannedQuantity.Should().Be(0);
        captured.PlannedWorkUnits.Should().Be(0);
        captured.IsRequiredGate.Should().BeTrue();
        captured.Assignees.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_Throws_WhenQuantityChildExceedsParentRemainingQuantity()
    {
        // Công đoạn #5 PlannedQuantity=1000; sibling con Quantity đã chiếm 960 — thêm con Quantity 50
        // nữa sẽ vượt 1000, phải bị chặn.
        SetupManagerWithMembers(11, 12, 13);
        _tasks.Setup(t => t.SumChildrenPlannedQuantityAsync(StageId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(960);
        var overflowCommand = new TaskCreateCommand(
            ProjectId: 1, DepartmentId: DeptId, ParentTaskId: StageId,
            Title: "Cắt khung - thêm", Description: null,
            ProgressType: ProgressType.Quantity,
            PlannedQuantity: 50, PlannedWorkUnits: 0, IsRequiredGate: false,
            Deadline: null, Priority: null, StepOrder: 1,
            AssigneeUserIds: new[] { 11 });

        var act = () => CreateSut().Handle(overflowCommand, CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
        _tasks.Verify(t => t.AddAsync(It.IsAny<MPS.Domain.Entities.ProjectTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_Throws403_WhenNotAssignedPmOfParentStage()
    {
        // Công đoạn #5 do PM khác (#999) phụ trách — user hiện tại (#7) không được tạo task con trong đó.
        _currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        _tasks.Setup(t => t.GetByIdAsync(StageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MPS.Domain.Entities.ProjectTask
            {
                Id = StageId, IsBatchParent = true, DepartmentId = DeptId, AssignedPmUserId = 999,
            });

        var act = () => CreateSut().Handle(Command(new[] { 11 }), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
