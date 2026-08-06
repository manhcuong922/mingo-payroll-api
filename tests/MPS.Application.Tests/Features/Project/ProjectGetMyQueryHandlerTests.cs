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
/// GET /api/Project/my — scope mở rộng (plan docs/plans/team-leader-orders-api.md §2.2):
///   P xuất hiện ⇔ P.ResponsibleUserId == userId  OR  ∃ Task T ∈ P.Tasks: T.Assignees ∋ userId
/// Verify:
/// - Nhánh assignee (công nhân, behavior cũ) không regress.
/// - Nhánh responsible (tổ trưởng) trả đơn kể cả khi user chưa có task nào.
/// - Trùng ở cả 2 nhánh → chỉ xuất hiện 1 lần.
/// - Tiến độ tính trên TRỌN cây task của đơn.
/// - Đơn đã huỷ (DeletedAt) bị ẩn khi includeCancelled=false ở cả hai nhánh.
/// - Không leak đơn ngoài scope (xưởng khác, không responsible + không assignee).
/// </summary>
public sealed class ProjectGetMyQueryHandlerTests
{
    private const int WorkerUserId = 55;
    private const int TeamLeaderUserId = 77;

    private readonly Mock<IProjectRepository> _projects = new();
    private readonly Mock<IProjectTaskRepository> _tasks = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    public ProjectGetMyQueryHandlerTests()
    {
        // Default: cả hai nhánh scope đều rỗng. Từng test override lại theo nhu cầu.
        _projects
            .Setup(p => p.GetByResponsibleUserIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(Array.Empty<MPS.Domain.Entities.Project>());
        _tasks
            .Setup(t => t.GetAssignedProjectIdsByUserAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<int>());
        _tasks
            .Setup(t => t.GetByProjectIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(Array.Empty<MPS.Domain.Entities.ProjectTask>());
    }

    private static MPS.Domain.Entities.Project Project(
        int id, string title, DateTime? deletedAt = null, int? responsibleUserId = null) => new()
    {
        Id = id,
        Title = title,
        Status = deletedAt is null ? ProjectStatus.Active : ProjectStatus.Cancelled,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
        Deadline = DateTime.UtcNow.AddDays(1),
        DeletedAt = deletedAt,
        ResponsibleUserId = responsibleUserId,
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

    // MYPRJ-02 (legacy): công nhân chỉ có task được gán, không phải responsible.
    [Fact]
    public async Task Assignee_Only_Returns_Project_With_Whole_Tree_Progress()
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

    // MYPRJ-01: tổ trưởng là ResponsibleUserId của đơn, không có task nào trong đơn.
    [Fact]
    public async Task Responsible_Only_Returns_Project_Even_When_User_Has_No_Assigned_Task()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(TeamLeaderUserId);
        _projects.Setup(p => p.GetByResponsibleUserIdAsync(TeamLeaderUserId, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new[] { Project(30, "Đơn 500 khung", responsibleUserId: TeamLeaderUserId) });

        var result = await CreateSut().Handle(new ProjectGetMyQuery(), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(30);
        result[0].ResponsibleUserId.Should().Be(TeamLeaderUserId);
        // Không call GetByIdAsync — đơn đã đến từ nhánh responsible, không cần re-fetch.
        _projects.Verify(p => p.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // MYPRJ-03: user vừa responsible vừa assignee → chỉ trả 1 lần (khử trùng theo ProjectId).
    [Fact]
    public async Task Responsible_And_Assignee_Overlap_Returns_Project_Only_Once()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(TeamLeaderUserId);
        _projects.Setup(p => p.GetByResponsibleUserIdAsync(TeamLeaderUserId, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new[] { Project(40, "Đơn dùng chung", responsibleUserId: TeamLeaderUserId) });
        _tasks.Setup(t => t.GetAssignedProjectIdsByUserAsync(TeamLeaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 40 });

        var result = await CreateSut().Handle(new ProjectGetMyQuery(), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(40);
        // Nhánh responsible đã load rồi → không fetch lại theo id qua nhánh assignee.
        _projects.Verify(p => p.GetByIdAsync(40, It.IsAny<CancellationToken>()), Times.Never);
    }

    // MYPRJ-02b (variant): user vừa responsible ở P1 vừa assignee ở P2 → trả cả 2, không trùng.
    [Fact]
    public async Task Union_Returns_Distinct_Projects_From_Both_Sources()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(TeamLeaderUserId);
        _projects.Setup(p => p.GetByResponsibleUserIdAsync(TeamLeaderUserId, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new[] { Project(50, "Đơn phụ trách", responsibleUserId: TeamLeaderUserId) });
        _tasks.Setup(t => t.GetAssignedProjectIdsByUserAsync(TeamLeaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 51 });
        _projects.Setup(p => p.GetByIdAsync(51, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Project(51, "Đơn có task assign"));

        var result = await CreateSut().Handle(new ProjectGetMyQuery(), CancellationToken.None);

        result.Select(r => r.Id).Should().BeEquivalentTo(new[] { 50, 51 });
    }

    // MYPRJ-07 + MYPRJ-09: không responsible, không assignee → không thấy đơn nào (không leak).
    [Fact]
    public async Task Returns_Empty_When_Neither_Responsible_Nor_Assignee()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(WorkerUserId);

        var result = await CreateSut().Handle(new ProjectGetMyQuery(), CancellationToken.None);

        result.Should().BeEmpty();
        _projects.Verify(p => p.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // MYPRJ-04: đơn cancelled, user là responsible, includeCancelled=false → không trả.
    [Fact]
    public async Task Responsible_Cancelled_Project_Hidden_When_IncludeCancelled_False()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(TeamLeaderUserId);
        // Repo tự lọc DeletedAt khi includeCancelled=false → mock trả rỗng.
        _projects.Setup(p => p.GetByResponsibleUserIdAsync(TeamLeaderUserId, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(Array.Empty<MPS.Domain.Entities.Project>());

        var result = await CreateSut().Handle(new ProjectGetMyQuery(IncludeCancelled: false), CancellationToken.None);

        result.Should().BeEmpty();
    }

    // MYPRJ-05: đơn cancelled, user là responsible, includeCancelled=true → có trả.
    [Fact]
    public async Task Responsible_Cancelled_Project_Included_When_IncludeCancelled_True()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(TeamLeaderUserId);
        _projects.Setup(p => p.GetByResponsibleUserIdAsync(TeamLeaderUserId, It.IsAny<CancellationToken>(), true))
            .ReturnsAsync(new[]
            {
                Project(60, "Đơn đã huỷ", deletedAt: DateTime.UtcNow, responsibleUserId: TeamLeaderUserId),
            });

        var result = await CreateSut().Handle(new ProjectGetMyQuery(IncludeCancelled: true), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(60);
    }

    // Legacy: nhánh assignee cũng phải lọc cancelled khi includeCancelled=false.
    [Fact]
    public async Task Assignee_Cancelled_Project_Hidden_When_IncludeCancelled_False()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(WorkerUserId);
        _tasks.Setup(t => t.GetAssignedProjectIdsByUserAsync(WorkerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 20 });
        _projects.Setup(p => p.GetByIdAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Project(20, "Đơn đã huỷ", deletedAt: DateTime.UtcNow));

        var result = await CreateSut().Handle(new ProjectGetMyQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    // MYPRJ-08 (một phần): không có current user → trả rỗng, không đụng repo/tasks.
    [Fact]
    public async Task Returns_Empty_When_No_Authenticated_User()
    {
        _currentUser.SetupGet(c => c.UserId).Returns((int?)null);

        var result = await CreateSut().Handle(new ProjectGetMyQuery(), CancellationToken.None);

        result.Should().BeEmpty();
        _projects.Verify(
            p => p.GetByResponsibleUserIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()),
            Times.Never);
        _tasks.Verify(
            t => t.GetAssignedProjectIdsByUserAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
