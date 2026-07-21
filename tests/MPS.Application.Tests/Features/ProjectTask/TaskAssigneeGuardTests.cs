using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.ProjectTask.Commands;
using MPS.Application.Services;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Tests.Features.ProjectTask;

/// <summary>
/// Backlog E2E 2026-07-21:
///   #24 — không được giao việc chồng: người đang giữ công đoạn CHƯA XONG (Todo/InProgress)
///         thì không nhận thêm task. Trước đây guard chỉ đếm InProgress nên Todo lọt hết.
///   #28 — không được giao việc cho người không còn hợp đồng lao động hiệu lực.
/// Kiểm qua TaskCreateCommandHandler vì TaskAssigneeGuard là internal static.
/// </summary>
public sealed class TaskAssigneeGuardTests
{
    private const int ManagerUserId = 7;
    private const int DeptId = 3;
    private const int StageId = 5;
    private const int WorkerUserId = 11;

    private readonly Mock<IProjectTaskRepository> _tasks = new();
    private readonly Mock<IProjectRepository> _projects = new();
    private readonly Mock<IDepartmentRepository> _departments = new();
    private readonly Mock<IDepartmentManagerAuthorizer> _authorizer = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IContractRepository> _contracts = new();

    public TaskAssigneeGuardTests()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        _authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _departments.Setup(d => d.GetMemberUserIdsAsync(DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { WorkerUserId, 12, 13 });
        _tasks.Setup(t => t.GetByIdAsync(StageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MPS.Domain.Entities.ProjectTask
            {
                Id = StageId, IsBatchParent = true, DepartmentId = DeptId,
                ProgressType = ProgressType.Quantity, PlannedQuantity = 1000,
            });
        _tasks.Setup(t => t.AddAsync(It.IsAny<MPS.Domain.Entities.ProjectTask>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        // Mặc định: còn HĐ hiệu lực + không giữ công đoạn nào dở.
        AllowContractsFor(WorkerUserId, 12, 13);
        _tasks.Setup(t => t.CountOpenByUserAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    private void AllowContractsFor(params int[] userIds) =>
        _contracts.Setup(c => c.GetUserIdsWithActiveContractAsync(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, DateTime _, CancellationToken _) =>
                ids.Where(userIds.Contains).ToList());

    private TaskCreateCommandHandler CreateSut() =>
        new(_tasks.Object, _projects.Object, _departments.Object, _authorizer.Object, _currentUser.Object,
            _contracts.Object);

    private static TaskCreateCommand Command(params int[] assignees) =>
        new(
            ProjectId: 1, DepartmentId: DeptId, ParentTaskId: StageId,
            Title: "Cắt khung", Description: null,
            ProgressType: ProgressType.Quantity,
            PlannedQuantity: 100, PlannedWorkUnits: 0, IsRequiredGate: false,
            Deadline: null, Priority: null, StepOrder: 1,
            AssigneeUserIds: assignees);

    [Fact]
    public async Task Create_Succeeds_WhenAssigneeIsFreeAndEmployed()
    {
        var id = await CreateSut().Handle(Command(WorkerUserId), CancellationToken.None);

        id.Should().Be(42);
    }

    [Fact]
    public async Task Create_Throws_WhenAssigneeStillHoldsAnOpenTask()
    {
        // #24: công nhân đang giữ 1 task Todo — CountOpenByUserAsync đếm cả Todo lẫn InProgress.
        _tasks.Setup(t => t.CountOpenByUserAsync(WorkerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var act = () => CreateSut().Handle(Command(WorkerUserId), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainValidationException>())
            .WithMessage("*chưa hoàn thành*");
        _tasks.Verify(t => t.AddAsync(It.IsAny<MPS.Domain.Entities.ProjectTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_Throws_WhenAssigneeHasNoActiveContract()
    {
        // #28: NV đã chấm dứt HĐ — không còn nằm trong tập "đang có HĐ hiệu lực".
        AllowContractsFor(12, 13);

        var act = () => CreateSut().Handle(Command(WorkerUserId), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainValidationException>())
            .WithMessage("*hợp đồng lao động hiệu lực*");
        _tasks.Verify(t => t.AddAsync(It.IsAny<MPS.Domain.Entities.ProjectTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
