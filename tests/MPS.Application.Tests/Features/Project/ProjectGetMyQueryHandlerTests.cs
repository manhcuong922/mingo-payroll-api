using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project.Queries;
using MPS.Application.Services;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.Project;

/// <summary>
/// GET /api/Project/my — đơn hàng công nhân tham gia. Verify:
/// - Chỉ trả đơn có task được gán cho chính user (scope theo TaskAssignee, không cần Project.Read.*)
/// - Tiến độ tính trên TRỌN cây task của đơn (kể cả task của người khác)
/// - Đơn đã huỷ (DeletedAt) bị ẩn khi includeCancelled=false
/// </summary>
public sealed class ProjectGetMyQueryHandlerTests
{
    private const int WorkerUserId = 55;

    private readonly Mock<IProjectRepository> _projects = new();
    private readonly Mock<IProjectTaskRepository> _tasks = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private static MPS.Domain.Entities.Project Project(int id, string title, DateTime? deletedAt = null) => new()
    {
        Id = id,
        Title = title,
        Status = ProjectStatus.Active,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
        Deadline = DateTime.UtcNow.AddDays(1),
        DeletedAt = deletedAt,
    };

    private static MPS.Domain.Entities.ProjectTask Leaf(
        int id, int projectId, int planned, int completed, TaskItemStatus status) => new()
    {
        Id = id,
        ProjectId = projectId,
        ProgressType = ProgressType.Quantity,
        PlannedQuantity = planned,
        CompletedQuantity = completed,
        PlannedWorkUnits = planned,
        Status = status,
    };

    private ProjectGetMyQueryHandler CreateSut() => new(
        _projects.Object, _tasks.Object, new ProgressService(), _currentUser.Object);

    [Fact]
    public async Task Returns_Only_Joined_Projects_With_Progress_Of_Whole_Tree()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(WorkerUserId);
        _tasks.Setup(t => t.GetAssignedProjectIdsByUserAsync(WorkerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 10 });
        _projects.Setup(p => p.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Project(10, "Đơn A"));
        // 1 task của worker (xong) + 1 task của đồng nghiệp (chưa xong) → tiến độ trọn cây = 50%.
        _tasks.Setup(t => t.GetByProjectIdsAsync(
                It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 10 })),
                It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new[]
            {
                Leaf(100, 10, planned: 100, completed: 100, TaskItemStatus.Done),
                Leaf(101, 10, planned: 100, completed: 0, TaskItemStatus.InProgress),
            });

        var result = await CreateSut().Handle(new ProjectGetMyQuery(), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(10);
        result[0].Title.Should().Be("Đơn A");
        result[0].CompletionPercent.Should().Be(50);
    }

    [Fact]
    public async Task Returns_Empty_When_User_Has_No_Assigned_Task()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(WorkerUserId);
        _tasks.Setup(t => t.GetAssignedProjectIdsByUserAsync(WorkerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<int>());

        var result = await CreateSut().Handle(new ProjectGetMyQuery(), CancellationToken.None);

        result.Should().BeEmpty();
        _projects.Verify(p => p.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Skips_Cancelled_Project_When_IncludeCancelled_False()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(WorkerUserId);
        _tasks.Setup(t => t.GetAssignedProjectIdsByUserAsync(WorkerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 20 });
        _projects.Setup(p => p.GetByIdAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Project(20, "Đơn đã huỷ", deletedAt: DateTime.UtcNow));

        var result = await CreateSut().Handle(new ProjectGetMyQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_Empty_When_No_Authenticated_User()
    {
        _currentUser.SetupGet(c => c.UserId).Returns((int?)null);

        var result = await CreateSut().Handle(new ProjectGetMyQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
