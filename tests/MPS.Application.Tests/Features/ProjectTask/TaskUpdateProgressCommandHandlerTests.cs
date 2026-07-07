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

public sealed class TaskUpdateProgressCommandHandlerTests
{
    private readonly Mock<IProjectTaskRepository> _tasks = new();
    private readonly Mock<IDepartmentManagerAuthorizer> _authorizer = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private const int ManagerUserId = 7;
    private const int DeptId = 3;

    private TaskUpdateProgressCommandHandler CreateSut() =>
        new(_tasks.Object, _authorizer.Object, _currentUser.Object);

    private static string Rv(byte[] b) => Convert.ToBase64String(b);

    // Guard mới (v7 mục 8) siết task lá theo đúng PM phụ trách công đoạn cha — các test dưới đây
    // không nhằm test phân quyền PM nên dùng GĐ (ProjectReadAll) để bypass, giữ tập trung vào business logic.
    private void SetupManager()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        _currentUser.Setup(c => c.HasPermission(MPS.Domain.Constants.Permissions.ProjectReadAll)).Returns(true);
        _authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private MPS.Domain.Entities.ProjectTask QuantityTask(int planned, int? parentId = null) =>
        new()
        {
            Id = 10, DepartmentId = DeptId, ProgressType = ProgressType.Quantity,
            IsBatchParent = false, PlannedQuantity = planned, Status = TaskItemStatus.Todo,
            ParentTaskId = parentId, RowVersion = new byte[] { 9 },
        };

    [Fact]
    public async Task UpdateProgress_SetsCompleted_AndMovesToInProgress()
    {
        SetupManager();
        var task = QuantityTask(100);
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.UpdateWithConcurrencyAsync(task, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateSut().Handle(new TaskUpdateProgressCommand(10, 40, Rv(task.RowVersion)), CancellationToken.None);

        task.CompletedQuantity.Should().Be(40);
        task.Status.Should().Be(TaskItemStatus.InProgress);
    }

    [Fact]
    public async Task UpdateProgress_Clamps_ToPlannedQuantity()
    {
        SetupManager();
        var task = QuantityTask(100);
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.UpdateWithConcurrencyAsync(task, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateSut().Handle(new TaskUpdateProgressCommand(10, 150, Rv(task.RowVersion)), CancellationToken.None);

        task.CompletedQuantity.Should().Be(100);   // clamp
    }

    [Fact]
    public async Task UpdateProgress_RecomputesParent_WhenChild()
    {
        SetupManager();
        var task = QuantityTask(100, parentId: 5);
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.UpdateWithConcurrencyAsync(task, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateSut().Handle(new TaskUpdateProgressCommand(10, 30, Rv(task.RowVersion)), CancellationToken.None);

        _tasks.Verify(t => t.RecomputeParentCompletedAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProgress_Throws_WhenMilestoneTask()
    {
        SetupManager();
        var task = QuantityTask(100);
        task.ProgressType = ProgressType.Milestone;
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var act = () => CreateSut().Handle(new TaskUpdateProgressCommand(10, 30, Rv(task.RowVersion)), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task UpdateProgress_Throws409_WhenRowVersionStale()
    {
        SetupManager();
        var task = QuantityTask(100);
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.UpdateWithConcurrencyAsync(task, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => CreateSut().Handle(new TaskUpdateProgressCommand(10, 30, Rv(task.RowVersion)), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }
}
