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
    private readonly Mock<IProductionAttachmentRepository> _attachments = new();
    private readonly Mock<IDepartmentManagerAuthorizer> _authorizer = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private const int ManagerUserId = 7;
    private const int DeptId = 3;

    private TaskCompleteCommandHandler CreateSut() =>
        new(_tasks.Object, _attachments.Object, _authorizer.Object, _currentUser.Object);

    private static string Rv(byte[] bytes) => Convert.ToBase64String(bytes);

    private void SetupManager()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        _authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private MPS.Domain.Entities.ProjectTask Task(DateTime? deadline, bool requiresHandover, int? parentId = null) =>
        new()
        {
            Id = 10,
            DepartmentId = DeptId,
            Deadline = deadline,
            RequiresHandover = requiresHandover,
            ParentTaskId = parentId,
            Status = TaskItemStatus.InProgress,
            RowVersion = new byte[] { 1, 2, 3 },
        };

    [Fact]
    public async Task Complete_SetsDone_AndNotLate_WhenBeforeDeadline()
    {
        SetupManager();
        var task = Task(DateTime.UtcNow.AddDays(2), requiresHandover: false);
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
        var task = Task(DateTime.UtcNow.AddDays(-1), requiresHandover: false);
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.UpdateWithConcurrencyAsync(task, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateSut().Handle(new TaskCompleteCommand(10, Rv(task.RowVersion)), CancellationToken.None);

        task.Status.Should().Be(TaskItemStatus.Done);
        task.WasLate.Should().BeTrue();
    }

    [Fact]
    public async Task Complete_Throws_WhenHandoverTaskHasNoAttachment()
    {
        SetupManager();
        var task = Task(DateTime.UtcNow.AddDays(2), requiresHandover: true);
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _attachments.Setup(a => a.CountByOwnerAsync(AttachmentOwnerType.TaskCompletion, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var act = () => CreateSut().Handle(new TaskCompleteCommand(10, Rv(task.RowVersion)), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
        _tasks.Verify(t => t.UpdateWithConcurrencyAsync(It.IsAny<MPS.Domain.Entities.ProjectTask>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Complete_Allows_WhenHandoverTaskHasAttachment()
    {
        SetupManager();
        var task = Task(DateTime.UtcNow.AddDays(2), requiresHandover: true);
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _attachments.Setup(a => a.CountByOwnerAsync(AttachmentOwnerType.TaskCompletion, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _tasks.Setup(t => t.UpdateWithConcurrencyAsync(task, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateSut().Handle(new TaskCompleteCommand(10, Rv(task.RowVersion)), CancellationToken.None);

        result.Should().BeTrue();
        task.Status.Should().Be(TaskItemStatus.Done);
    }

    [Fact]
    public async Task Complete_TriesPersistParentDone_WhenTaskHasParent()
    {
        SetupManager();
        var task = Task(DateTime.UtcNow.AddDays(2), requiresHandover: false, parentId: 99);
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.UpdateWithConcurrencyAsync(task, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateSut().Handle(new TaskCompleteCommand(10, Rv(task.RowVersion)), CancellationToken.None);

        _tasks.Verify(t => t.TrySetParentDoneAsync(99, It.IsAny<DateTime>(), ManagerUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Complete_Throws403_WhenNotManagerOfDepartment()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        _authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var task = Task(DateTime.UtcNow.AddDays(2), requiresHandover: false);
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        var act = () => CreateSut().Handle(new TaskCompleteCommand(10, Rv(task.RowVersion)), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Complete_Throws409_WhenRowVersionStale()
    {
        SetupManager();
        var task = Task(DateTime.UtcNow.AddDays(2), requiresHandover: false);
        _tasks.Setup(t => t.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _tasks.Setup(t => t.UpdateWithConcurrencyAsync(task, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => CreateSut().Handle(new TaskCompleteCommand(10, Rv(task.RowVersion)), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }
}
