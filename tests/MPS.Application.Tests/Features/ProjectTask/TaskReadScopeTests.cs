using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;
using Xunit;
using ProjectTaskEntity = MPS.Domain.Entities.ProjectTask;
using TaskAssignee = MPS.Domain.Entities.TaskAssignee;

namespace MPS.Application.Tests.Features.ProjectTask;

// refactor R/S: bỏ per-stage PM. Đọc task theo permission + scope xưởng: quản lý xưởng (Read.Department/
// Read.FullDepartment + IsManagerOf) xem trọn cây xưởng mình; ngoài xưởng → 403.
public sealed class TaskReadScopeTests
{
    private const int ManagerUserId = 20;
    private const int DeptId = 3;

    private static ProjectTaskEntity Node(int id, bool isContainer) => new()
    {
        Id = id,
        IsBatchParent = isContainer,
        DepartmentId = DeptId,
        Status = TaskItemStatus.InProgress,
        ProgressType = ProgressType.Quantity,
    };

    [Fact]
    public async Task EnsureCanReadTaskAsync_DepartmentManager_AllowedOnAnyNodeInOwnDepartment()
    {
        var tasks = new Mock<IProjectTaskRepository>();
        var authorizer = new Mock<IDepartmentManagerAuthorizer>();
        var currentUser = new Mock<ICurrentUserService>();

        currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        currentUser.Setup(c => c.HasPermission(Permissions.TaskReadDepartment)).Returns(true);
        authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await FluentActions
            .Awaiting(() => ProjectScopeGuard.EnsureCanReadTaskAsync(
                Node(200, isContainer: true), tasks.Object, currentUser.Object, authorizer.Object, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanReadTaskAsync_FullDepartmentPermission_AllowedInOwnDepartment()
    {
        var tasks = new Mock<IProjectTaskRepository>();
        var authorizer = new Mock<IDepartmentManagerAuthorizer>();
        var currentUser = new Mock<ICurrentUserService>();

        currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        currentUser.Setup(c => c.HasPermission(Permissions.TaskReadFullDepartment)).Returns(true);
        authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await FluentActions
            .Awaiting(() => ProjectScopeGuard.EnsureCanReadTaskAsync(
                Node(200, isContainer: true), tasks.Object, currentUser.Object, authorizer.Object, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanReadTaskAsync_ForbiddenOutsideOwnDepartment()
    {
        var tasks = new Mock<IProjectTaskRepository>();
        var authorizer = new Mock<IDepartmentManagerAuthorizer>();
        var currentUser = new Mock<ICurrentUserService>();

        currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        currentUser.Setup(c => c.HasPermission(Permissions.TaskReadFullDepartment)).Returns(true);
        authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);   // không quản lý xưởng này

        await FluentActions
            .Awaiting(() => ProjectScopeGuard.EnsureCanReadTaskAsync(
                Node(300, isContainer: true), tasks.Object, currentUser.Object, authorizer.Object, CancellationToken.None))
            .Should().ThrowAsync<MPS.Domain.Exceptions.ForbiddenAccessException>();
    }

    // ===== Công nhân (Task.Read.Assigned) — policy mới: CHỈ xem task được gán trực tiếp
    //       + tổ tiên (batch parent chứa descendant của mình). Leaf người khác luôn bị chặn,
    //       kể cả trong đơn mình cùng làm. =====

    private const int WorkerUserId = 55;
    private const int JoinedProjectId = 71;
    private const int OtherProjectId = 72;

    /// <summary>Task của NGƯỜI KHÁC (Assignees rỗng với worker) trong 1 đơn cụ thể.</summary>
    private static ProjectTaskEntity TeammateTask(int id, int projectId, bool isConfidential = false, bool isBatchParent = false, int? parentTaskId = null) => new()
    {
        Id = id,
        ProjectId = projectId,
        DepartmentId = DeptId,
        Status = TaskItemStatus.InProgress,
        ProgressType = ProgressType.Quantity,
        IsConfidential = isConfidential,
        IsBatchParent = isBatchParent,
        ParentTaskId = parentTaskId,
        CreatedByUserId = ManagerUserId,
    };

    /// <summary>Task được gán cho worker.</summary>
    private static ProjectTaskEntity MyLeaf(int id, int projectId, int? parentTaskId = null)
    {
        var t = TeammateTask(id, projectId, parentTaskId: parentTaskId);
        t.Assignees = new List<TaskAssignee>
        {
            new() { TaskId = id, UserId = WorkerUserId, AssignedAt = DateTime.UtcNow },
        };
        return t;
    }

    private static (Mock<IProjectTaskRepository> Tasks, Mock<IDepartmentManagerAuthorizer> Authorizer, Mock<ICurrentUserService> CurrentUser)
        WorkerContext()
    {
        var tasks = new Mock<IProjectTaskRepository>();
        var authorizer = new Mock<IDepartmentManagerAuthorizer>();
        var currentUser = new Mock<ICurrentUserService>();

        currentUser.SetupGet(c => c.UserId).Returns(WorkerUserId);
        currentUser.Setup(c => c.HasPermission(Permissions.TaskReadAssigned)).Returns(true);
        authorizer.Setup(a => a.IsManagerOfAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return (tasks, authorizer, currentUser);
    }

    [Fact]
    public async Task EnsureCanReadTaskAsync_Worker_ForbiddenOnTeammateLeafEvenInJoinedProject()
    {
        // Policy mới: công nhân KHÔNG được xem task lá của đồng nghiệp, kể cả cùng đơn.
        var (tasks, authorizer, currentUser) = WorkerContext();
        var teammateLeaf = TeammateTask(400, JoinedProjectId, isBatchParent: false);

        await FluentActions
            .Awaiting(() => ProjectScopeGuard.EnsureCanReadTaskAsync(
                teammateLeaf, tasks.Object, currentUser.Object, authorizer.Object, CancellationToken.None))
            .Should().ThrowAsync<MPS.Domain.Exceptions.ForbiddenAccessException>()
            .Where(ex => ex.ErrorCode == ProjectScopeGuard.TaskNotAssignedErrorCode);
    }

    [Fact]
    public async Task EnsureCanReadTaskAsync_Worker_AllowedOnAssignedLeaf()
    {
        var (tasks, authorizer, currentUser) = WorkerContext();
        var myLeaf = MyLeaf(500, JoinedProjectId, parentTaskId: 600);

        await FluentActions
            .Awaiting(() => ProjectScopeGuard.EnsureCanReadTaskAsync(
                myLeaf, tasks.Object, currentUser.Object, authorizer.Object, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanReadTaskAsync_Worker_AllowedOnBatchParentWithAssignedDescendant()
    {
        // Batch parent (id=600) chứa task lá 500 được gán cho worker → mở được node công đoạn.
        var (tasks, authorizer, currentUser) = WorkerContext();
        var stage = TeammateTask(600, JoinedProjectId, isBatchParent: true);
        var myLeaf = MyLeaf(500, JoinedProjectId, parentTaskId: 600);
        var siblingLeaf = TeammateTask(501, JoinedProjectId, parentTaskId: 600);

        tasks.Setup(t => t.GetByProjectIdAsync(JoinedProjectId, It.IsAny<CancellationToken>(), true))
            .ReturnsAsync(new[] { stage, myLeaf, siblingLeaf });

        await FluentActions
            .Awaiting(() => ProjectScopeGuard.EnsureCanReadTaskAsync(
                stage, tasks.Object, currentUser.Object, authorizer.Object, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanReadTaskAsync_Worker_ForbiddenOnBatchParentWithoutAssignedDescendant()
    {
        // Batch parent chỉ chứa leaf của người khác → worker không được mở.
        var (tasks, authorizer, currentUser) = WorkerContext();
        var stage = TeammateTask(601, JoinedProjectId, isBatchParent: true);
        var siblingLeaf = TeammateTask(510, JoinedProjectId, parentTaskId: 601);

        tasks.Setup(t => t.GetByProjectIdAsync(JoinedProjectId, It.IsAny<CancellationToken>(), true))
            .ReturnsAsync(new[] { stage, siblingLeaf });

        await FluentActions
            .Awaiting(() => ProjectScopeGuard.EnsureCanReadTaskAsync(
                stage, tasks.Object, currentUser.Object, authorizer.Object, CancellationToken.None))
            .Should().ThrowAsync<MPS.Domain.Exceptions.ForbiddenAccessException>()
            .Where(ex => ex.ErrorCode == ProjectScopeGuard.TaskNotAssignedErrorCode);
    }

    [Fact]
    public async Task EnsureCanReadTaskAsync_Worker_ForbiddenOnLeafInProjectNotJoined()
    {
        var (tasks, authorizer, currentUser) = WorkerContext();

        await FluentActions
            .Awaiting(() => ProjectScopeGuard.EnsureCanReadTaskAsync(
                TeammateTask(401, OtherProjectId), tasks.Object, currentUser.Object, authorizer.Object, CancellationToken.None))
            .Should().ThrowAsync<MPS.Domain.Exceptions.ForbiddenAccessException>();
    }

    [Fact]
    public async Task EnsureCanReadTaskAsync_Worker_StillForbiddenOnConfidentialTeammateTask()
    {
        var (tasks, authorizer, currentUser) = WorkerContext();

        await FluentActions
            .Awaiting(() => ProjectScopeGuard.EnsureCanReadTaskAsync(
                TeammateTask(402, JoinedProjectId, isConfidential: true),
                tasks.Object, currentUser.Object, authorizer.Object, CancellationToken.None))
            .Should().ThrowAsync<MPS.Domain.Exceptions.ForbiddenAccessException>(
                "task nhạy cảm chỉ dành cho assignee/người tạo/Task.Read.Confidential");
    }

    [Fact]
    public async Task EnsureCanReadTaskAsync_Worker_ForbiddenOnInternalTaskWithoutProject()
    {
        var (tasks, authorizer, currentUser) = WorkerContext();
        var internalTask = TeammateTask(403, JoinedProjectId);
        internalTask.ProjectId = null;   // việc nội bộ, không thuộc đơn nào

        await FluentActions
            .Awaiting(() => ProjectScopeGuard.EnsureCanReadTaskAsync(
                internalTask, tasks.Object, currentUser.Object, authorizer.Object, CancellationToken.None))
            .Should().ThrowAsync<MPS.Domain.Exceptions.ForbiddenAccessException>();
    }

    // ===== ComputeVisibleTaskIdsForAssignedUser — helper thuần =====

    [Fact]
    public void ComputeVisibleTaskIdsForAssignedUser_ReturnsAssignedLeavesAndAncestorsOnly()
    {
        // Cây: 1 (root batch) → 2 (stage batch) → {3 mine, 4 teammate}; 10 (stage) → 11 teammate.
        var root = new ProjectTaskEntity { Id = 1, IsBatchParent = true };
        var stage = new ProjectTaskEntity { Id = 2, IsBatchParent = true, ParentTaskId = 1 };
        var mine = new ProjectTaskEntity
        {
            Id = 3,
            ParentTaskId = 2,
            Assignees = new List<TaskAssignee> { new() { TaskId = 3, UserId = WorkerUserId } },
        };
        var teammate = new ProjectTaskEntity { Id = 4, ParentTaskId = 2 };
        var otherStage = new ProjectTaskEntity { Id = 10, IsBatchParent = true };
        var otherLeaf = new ProjectTaskEntity { Id = 11, ParentTaskId = 10 };

        var visible = ProjectScopeGuard.ComputeVisibleTaskIdsForAssignedUser(
            new[] { root, stage, mine, teammate, otherStage, otherLeaf }, WorkerUserId);

        visible.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }
}
