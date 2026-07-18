using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.ProjectTask.Commands;
using MPS.Application.Services;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.ProjectTask;

public sealed class TaskCompleteCommandHandlerTests
{
    private readonly Mock<IProjectTaskRepository> _tasks = new();
    private readonly Mock<IProjectRepository> _projects = new();
    private readonly Mock<IDepartmentManagerAuthorizer> _authorizer = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private const int ManagerUserId = 7;
    private const int DeptId = 3;

    private TaskCompleteCommandHandler CreateSut() =>
        new(_tasks.Object, _projects.Object, _authorizer.Object, _currentUser.Object);

    private static string Rv(byte[] bytes) => Convert.ToBase64String(bytes);

    // Guard mới (v7 mục 8) siết task lá theo đúng PM phụ trách công đoạn cha — các test dưới đây
    // không nhằm test phân quyền PM nên dùng GĐ (ProjectReadAll) để bypass, giữ tập trung vào business logic.
    private void SetupManager()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        _currentUser.Setup(c => c.HasPermission(MPS.Domain.Constants.Permissions.ProjectReadAll)).Returns(true);
        _authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private MPS.Domain.Entities.ProjectTask Task(
        DateTime? deadline, int? parentId = null,
        ProgressType progressType = ProgressType.Quantity, int plannedQuantity = 200, int completedQuantity = 80) =>
        new()
        {
            Id = 10,
            DepartmentId = DeptId,
            Deadline = deadline,
            ParentTaskId = parentId,
            Status = TaskItemStatus.InProgress,
            ProgressType = progressType,
            PlannedQuantity = plannedQuantity,
            CompletedQuantity = completedQuantity,
            RowVersion = new byte[] { 1, 2, 3 },
        };

    [Fact]
    public async Task Complete_SetsDone_AndNotLate_WhenBeforeDeadline()
    {
        SetupManager();
        var task = Task(DateTime.UtcNow.AddDays(2));
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.UpdateWithConcurrencyAsync(task, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateSut().Handle(new TaskCompleteCommand(10, Rv(task.RowVersion)), CancellationToken.None);

        result.Should().BeTrue();
        task.Status.Should().Be(TaskItemStatus.Done);
        task.WasLate.Should().BeFalse();
        task.CompletedByUserId.Should().Be(ManagerUserId);
    }

    [Fact]
    public async Task Complete_SetsWasLate_WhenPastDeadline()
    {
        SetupManager();
        var task = Task(DateTime.UtcNow.AddDays(-1));
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.UpdateWithConcurrencyAsync(task, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateSut().Handle(new TaskCompleteCommand(10, Rv(task.RowVersion)), CancellationToken.None);

        task.Status.Should().Be(TaskItemStatus.Done);
        task.WasLate.Should().BeTrue();
    }

    [Fact]
    public async Task Complete_TriesPersistParentDone_WhenTaskHasParent()
    {
        SetupManager();
        var task = Task(DateTime.UtcNow.AddDays(2), parentId: 99);
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.UpdateWithConcurrencyAsync(task, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateSut().Handle(new TaskCompleteCommand(10, Rv(task.RowVersion)), CancellationToken.None);

        // Rollup đệ quy lên tổ tiên từ chính leaf vừa Done (refactor).
        _tasks.Verify(t => t.RollupAncestorsAsync(10, It.IsAny<DateTime>(), ManagerUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Complete_SetsCompletedQuantityToPlanned_WhenProgressTypeIsQuantity()
    {
        SetupManager();
        var task = Task(DateTime.UtcNow.AddDays(2), plannedQuantity: 200, completedQuantity: 80);
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.UpdateWithConcurrencyAsync(task, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateSut().Handle(new TaskCompleteCommand(10, Rv(task.RowVersion)), CancellationToken.None);

        task.CompletedQuantity.Should().Be(200);
    }

    [Fact]
    public async Task Complete_LeavesCompletedQuantityAtZero_WhenProgressTypeIsGate()
    {
        // v8 (docs/plans/plan.md mục 4.2): Gate (Milestone) hoàn thành chỉ đổi Status — không có sản lượng
        // để set, CompletedQuantity giữ nguyên 0 (không quy đổi từ Weight nữa).
        SetupManager();
        var task = Task(
            DateTime.UtcNow.AddDays(2),
            progressType: ProgressType.Milestone, plannedQuantity: 0, completedQuantity: 0);
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.UpdateWithConcurrencyAsync(task, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateSut().Handle(new TaskCompleteCommand(10, Rv(task.RowVersion)), CancellationToken.None);

        result.Should().BeTrue();
        task.Status.Should().Be(TaskItemStatus.Done);
        task.CompletedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Complete_Throws403_WhenNotResponsibleNorPlanningManager()
    {
        // refactor R/S: chỉ tổ trưởng phụ trách đơn (ResponsibleUserId) / Planning Manager / GĐ mới hoàn thành được.
        _currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);   // user #7 không phải tổ trưởng đơn
        var task = Task(DateTime.UtcNow.AddDays(2));
        task.ProjectId = 50;
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _projects.Setup(p => p.GetByIdAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MPS.Domain.Entities.Project { Id = 50, DepartmentId = DeptId, ResponsibleUserId = 999 });

        var act = () => CreateSut().Handle(new TaskCompleteCommand(10, Rv(task.RowVersion)), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Complete_Throws409_WhenRowVersionStale()
    {
        SetupManager();
        var task = Task(DateTime.UtcNow.AddDays(2));
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.UpdateWithConcurrencyAsync(task, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => CreateSut().Handle(new TaskCompleteCommand(10, Rv(task.RowVersion)), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }
}
