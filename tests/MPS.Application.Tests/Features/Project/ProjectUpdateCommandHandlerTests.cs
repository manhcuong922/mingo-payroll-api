using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project.Commands;
using MPS.Application.Services;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.Project;

// Sau bug time-cascade: null Deadline/StartDate trong payload = "không đổi" (partial-update). Nếu không,
// mọi Title-only update từ client (JSON default null cho Deadline) sẽ wipe deadline đơn + cascade toàn bộ
// task về null — chính là triệu chứng "áp dụng thời gian gặp vấn đề" user báo.
public sealed class ProjectUpdateCommandHandlerTests
{
    private readonly Mock<IProjectRepository> _projects = new();
    private readonly Mock<IProjectTaskRepository> _tasks = new();
    private readonly Mock<IDepartmentManagerAuthorizer> _authorizer = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private const int UserId = 7;
    private const int DeptId = 3;
    private const int ProjectId = 1;

    private ProjectUpdateCommandHandler CreateSut() =>
        new(_projects.Object, _tasks.Object, _authorizer.Object, _currentUser.Object);

    private MPS.Domain.Entities.Project ArrangeProject(DateOnly start, DateTime? deadline)
    {
        _currentUser.SetupGet(c => c.UserId).Returns(UserId);
        _authorizer.Setup(a => a.IsManagerOfAsync(UserId, DeptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var project = new MPS.Domain.Entities.Project
        {
            Id = ProjectId, DepartmentId = DeptId, Title = "Đơn A", Status = ProjectStatus.Active,
            StartDate = start, Deadline = deadline,
        };
        _projects.Setup(p => p.GetByIdAsync(ProjectId, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _projects.Setup(p => p.UpdateAsync(project, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return project;
    }

    [Fact]
    public async Task Update_TitleOnly_KeepsDeadline_AndDoesNotCascade()
    {
        // Client gửi Title mới, không kèm StartDate/Deadline (deserialize null cho cả 2).
        // Handler PHẢI giữ nguyên deadline & KHÔNG gọi cascade — nếu không, task deadlines sẽ bị wipe.
        var deadline = new DateTime(2026, 3, 1);
        var project = ArrangeProject(new DateOnly(2026, 1, 1), deadline);

        var cmd = new ProjectUpdateCommand(ProjectId, "Đơn A - đổi tên", Description: null,
            StartDate: null, Deadline: null, Status: null);
        var ok = await CreateSut().Handle(cmd, CancellationToken.None);

        ok.Should().BeTrue();
        project.Title.Should().Be("Đơn A - đổi tên");
        project.Deadline.Should().Be(deadline);
        _tasks.Verify(t => t.RecomputeDeadlinesFromRatioAsync(
            It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Update_DeadlineChanged_TriggersCascade_WithNewTimeline()
    {
        var oldDeadline = new DateTime(2026, 3, 1);
        var newDeadline = new DateTime(2026, 4, 15);
        var start = new DateOnly(2026, 1, 1);
        var project = ArrangeProject(start, oldDeadline);

        var cmd = new ProjectUpdateCommand(ProjectId, "Đơn A", Description: null,
            StartDate: null, Deadline: newDeadline, Status: null);
        var ok = await CreateSut().Handle(cmd, CancellationToken.None);

        ok.Should().BeTrue();
        project.Deadline.Should().Be(newDeadline);
        _tasks.Verify(t => t.RecomputeDeadlinesFromRatioAsync(
            ProjectId, start, newDeadline, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_StartDateChanged_TriggersCascade_WithNewTimeline()
    {
        var oldStart = new DateOnly(2026, 1, 1);
        var newStart = new DateOnly(2026, 1, 15);
        var deadline = new DateTime(2026, 3, 1);
        var project = ArrangeProject(oldStart, deadline);

        var cmd = new ProjectUpdateCommand(ProjectId, "Đơn A", Description: null,
            StartDate: newStart, Deadline: null, Status: null);
        var ok = await CreateSut().Handle(cmd, CancellationToken.None);

        ok.Should().BeTrue();
        project.StartDate.Should().Be(newStart);
        project.Deadline.Should().Be(deadline);   // giữ nguyên deadline
        _tasks.Verify(t => t.RecomputeDeadlinesFromRatioAsync(
            ProjectId, newStart, deadline, It.IsAny<CancellationToken>()), Times.Once);
    }
}
