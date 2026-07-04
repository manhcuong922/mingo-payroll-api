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
    private readonly Mock<IDepartmentRepository> _departments = new();
    private readonly Mock<IDepartmentManagerAuthorizer> _authorizer = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private const int ManagerUserId = 7;
    private const int DeptId = 3;

    private TaskCreateCommandHandler CreateSut() =>
        new(_tasks.Object, _departments.Object, _authorizer.Object, _currentUser.Object);

    private void SetupManagerWithMembers(params int[] memberUserIds)
    {
        _currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        _authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _departments.Setup(d => d.GetMemberUserIdsAsync(DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberUserIds);
        _tasks.Setup(t => t.AddAsync(It.IsAny<MPS.Domain.Entities.ProjectTask>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
    }

    private static TaskCreateCommand Command(IReadOnlyList<int> assignees) =>
        new(
            ProjectId: 1, DepartmentId: DeptId, ParentTaskId: null,
            Title: "Cắt khung", Description: null,
            ProgressType: ProgressType.Quantity,
            PlannedQuantity: 100, Weight: 0,
            RequiresHandover: false, Deadline: null, Priority: null, StepOrder: 1,
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
    public async Task Create_Throws403_WhenNotManagerOfDepartment()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        _authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => CreateSut().Handle(Command(new[] { 11 }), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
