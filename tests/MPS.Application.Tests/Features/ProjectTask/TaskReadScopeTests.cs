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

    // ===== Công nhân (Task.Read.Assigned) xem task của đồng nghiệp trong đơn mình cùng làm =====

    private const int WorkerUserId = 55;
    private const int JoinedProjectId = 71;
    private const int OtherProjectId = 72;

    /// <summary>Task của NGƯỜI KHÁC (Assignees rỗng với worker) trong 1 đơn cụ thể.</summary>
    private static ProjectTaskEntity TeammateTask(int id, int projectId, bool isConfidential = false) => new()
    {
        Id = id,
        ProjectId = projectId,
        DepartmentId = DeptId,
        Status = TaskItemStatus.InProgress,
        ProgressType = ProgressType.Quantity,
        IsConfidential = isConfidential,
        CreatedByUserId = ManagerUserId,
    };

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
        tasks.Setup(t => t.HasAssignedTaskInProjectAsync(WorkerUserId, JoinedProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        tasks.Setup(t => t.HasAssignedTaskInProjectAsync(WorkerUserId, OtherProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return (tasks, authorizer, currentUser);
    }

    [Fact]
    public async Task EnsureCanReadTaskAsync_Worker_AllowedOnTeammateTaskInJoinedProject()
    {
        var (tasks, authorizer, currentUser) = WorkerContext();

        await FluentActions
            .Awaiting(() => ProjectScopeGuard.EnsureCanReadTaskAsync(
                TeammateTask(400, JoinedProjectId), tasks.Object, currentUser.Object, authorizer.Object, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanReadTaskAsync_Worker_ForbiddenOnProjectNotJoined()
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
}
