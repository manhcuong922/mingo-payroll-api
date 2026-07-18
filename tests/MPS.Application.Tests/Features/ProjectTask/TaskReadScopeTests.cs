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
}
