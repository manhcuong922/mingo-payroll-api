using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project;
using MPS.Application.Features.ProjectTask.Queries;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;
using Xunit;
using ProjectTaskEntity = MPS.Domain.Entities.ProjectTask;

namespace MPS.Application.Tests.Features.ProjectTask;

// Bao phủ đúng ràng buộc phân quyền quan sát: PM (Task.Read.Department) chỉ thấy Công đoạn được gán
// cho chính mình + task con (2 tầng hiệu quả); vai trò xem đầy đủ 1 xưởng (Task.Read.FullDepartment,
// vd. DepartmentManager) thấy mọi Công đoạn + task con trong xưởng (3 tầng), không bị lọc theo
// AssignedPmUserId. Permission-based: không hardcode role name ở bất kỳ đâu trong logic.
public sealed class TaskReadScopeTests
{
    private const int PmUserId = 7;
    private const int OtherPmUserId = 8;
    private const int ManagerUserId = 20;
    private const int DeptId = 3;

    private static ProjectTaskEntity Stage(int id, int? assignedPmUserId) => new()
    {
        Id = id,
        IsBatchParent = true,
        AssignedPmUserId = assignedPmUserId,
        DepartmentId = DeptId,
        Status = TaskItemStatus.InProgress,
        ProgressType = ProgressType.Quantity,
    };

    private static ProjectTaskEntity Leaf(int id, int parentId) => new()
    {
        Id = id,
        ParentTaskId = parentId,
        IsBatchParent = false,
        DepartmentId = DeptId,
        Status = TaskItemStatus.InProgress,
        ProgressType = ProgressType.Quantity,
    };

    [Fact]
    public void FilterByAssignedPm_Pm_OnlySeesOwnStageAndItsChildren()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(PmUserId);
        currentUser.Setup(c => c.HasPermission(Permissions.TaskReadAll)).Returns(false);
        currentUser.Setup(c => c.HasPermission(Permissions.TaskReadFullDepartment)).Returns(false);

        var myStage = Stage(100, PmUserId);
        var otherStage = Stage(200, OtherPmUserId);
        var items = new List<ProjectTaskEntity> { myStage, otherStage, Leaf(101, 100), Leaf(201, 200) };

        var result = TaskGetByDepartmentQueryHandler.FilterByAssignedPm(items, currentUser.Object);

        result.Select(t => t.Id).Should().BeEquivalentTo(new[] { 100, 101 });
    }

    [Fact]
    public void FilterByAssignedPm_FullDepartmentPermission_SeesAllStagesRegardlessOfAssignedPm()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        currentUser.Setup(c => c.HasPermission(Permissions.TaskReadAll)).Returns(false);
        currentUser.Setup(c => c.HasPermission(Permissions.TaskReadFullDepartment)).Returns(true);

        var stageA = Stage(100, PmUserId);
        var stageB = Stage(200, OtherPmUserId);
        var items = new List<ProjectTaskEntity> { stageA, stageB, Leaf(101, 100), Leaf(201, 200) };

        var result = TaskGetByDepartmentQueryHandler.FilterByAssignedPm(items, currentUser.Object);

        result.Select(t => t.Id).Should().BeEquivalentTo(new[] { 100, 200, 101, 201 });
    }

    [Fact]
    public async Task EnsureCanReadTaskAsync_Pm_ForbiddenOnStageAssignedToOtherPm()
    {
        var tasks = new Mock<IProjectTaskRepository>();
        var authorizer = new Mock<IDepartmentManagerAuthorizer>();
        var currentUser = new Mock<ICurrentUserService>();

        currentUser.SetupGet(c => c.UserId).Returns(PmUserId);
        currentUser.Setup(c => c.HasPermission(Permissions.TaskReadAll)).Returns(false);
        currentUser.Setup(c => c.HasPermission(Permissions.TaskReadFullDepartment)).Returns(false);
        currentUser.Setup(c => c.HasPermission(Permissions.TaskReadDepartment)).Returns(true);
        authorizer.Setup(a => a.IsManagerOfAsync(PmUserId, DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var otherStage = Stage(200, OtherPmUserId);

        await FluentActions
            .Awaiting(() => ProjectScopeGuard.EnsureCanReadTaskAsync(
                otherStage, tasks.Object, currentUser.Object, authorizer.Object, CancellationToken.None))
            .Should().ThrowAsync<MPS.Domain.Exceptions.ForbiddenAccessException>();
    }

    [Fact]
    public async Task EnsureCanReadTaskAsync_FullDepartmentPermission_AllowedOnStageAssignedToAnyPm()
    {
        var tasks = new Mock<IProjectTaskRepository>();
        var authorizer = new Mock<IDepartmentManagerAuthorizer>();
        var currentUser = new Mock<ICurrentUserService>();

        currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        currentUser.Setup(c => c.HasPermission(Permissions.TaskReadAll)).Returns(false);
        currentUser.Setup(c => c.HasPermission(Permissions.TaskReadFullDepartment)).Returns(true);
        authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var otherStage = Stage(200, OtherPmUserId);

        await FluentActions
            .Awaiting(() => ProjectScopeGuard.EnsureCanReadTaskAsync(
                otherStage, tasks.Object, currentUser.Object, authorizer.Object, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanReadTaskAsync_FullDepartmentPermission_ForbiddenOutsideOwnDepartment()
    {
        var tasks = new Mock<IProjectTaskRepository>();
        var authorizer = new Mock<IDepartmentManagerAuthorizer>();
        var currentUser = new Mock<ICurrentUserService>();

        currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        currentUser.Setup(c => c.HasPermission(Permissions.TaskReadAll)).Returns(false);
        currentUser.Setup(c => c.HasPermission(Permissions.TaskReadFullDepartment)).Returns(true);
        // Không quản lý DeptId này — chỉ full-scope trong xưởng mình, không phải toàn công ty.
        authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var otherDeptStage = Stage(300, OtherPmUserId);

        await FluentActions
            .Awaiting(() => ProjectScopeGuard.EnsureCanReadTaskAsync(
                otherDeptStage, tasks.Object, currentUser.Object, authorizer.Object, CancellationToken.None))
            .Should().ThrowAsync<MPS.Domain.Exceptions.ForbiddenAccessException>();
    }
}
