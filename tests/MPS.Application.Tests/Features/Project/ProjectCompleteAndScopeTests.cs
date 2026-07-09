using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project.Commands;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.Project;

public sealed class ProjectCompleteAndScopeTests
{
    private readonly Mock<IProjectRepository> _projects = new();
    private readonly Mock<IProjectTaskRepository> _tasks = new();
    private readonly Mock<IDepartmentRepository> _departments = new();
    private readonly Mock<IDepartmentManagerAuthorizer> _authorizer = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private const int ManagerUserId = 7;
    private const int DeptId = 3;
    private const int TeamLeaderUserId = 9;

    // ===== ProjectComplete =====

    private ProjectCompleteCommandHandler CompleteSut() =>
        new(_projects.Object, _tasks.Object, _authorizer.Object, _currentUser.Object);

    [Fact]
    public async Task Complete_SetsCompleted_WithNote_WhenManager()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        _authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var project = new MPS.Domain.Entities.Project { Id = 1, DepartmentId = DeptId, Status = ProjectStatus.Active };
        _projects.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _projects.Setup(p => p.UpdateAsync(project, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _tasks.Setup(t => t.GetByProjectIdAsync(1, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(Array.Empty<MPS.Domain.Entities.ProjectTask>());

        var result = await CompleteSut().Handle(new ProjectCompleteCommand(1, "Rút kinh nghiệm: máy hỏng 1 buổi."), CancellationToken.None);

        result.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Completed);
        project.CompletedByUserId.Should().Be(ManagerUserId);
        project.CompletionNote.Should().Be("Rút kinh nghiệm: máy hỏng 1 buổi.");
    }

    [Fact]
    public async Task Complete_Throws403_WhenNotManagerOfProjectDepartment()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        _authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var project = new MPS.Domain.Entities.Project { Id = 1, DepartmentId = DeptId, Status = ProjectStatus.Active };
        _projects.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        var act = () => CompleteSut().Handle(new ProjectCompleteCommand(1, null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Complete_Throws_WhenRequiredStageNotDone()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        _authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var project = new MPS.Domain.Entities.Project { Id = 1, DepartmentId = DeptId, Status = ProjectStatus.Active };
        _projects.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _tasks.Setup(t => t.GetByProjectIdAsync(1, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new[]
            {
                new MPS.Domain.Entities.ProjectTask
                {
                    Id = 10, IsBatchParent = true, Title = "Cắt khung", Status = TaskItemStatus.InProgress,
                },
            });

        var act = () => CompleteSut().Handle(new ProjectCompleteCommand(1, null), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
        _projects.Verify(p => p.UpdateAsync(It.IsAny<MPS.Domain.Entities.Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Complete_Throws_WhenRequiredGateNotDone()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        _authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var project = new MPS.Domain.Entities.Project { Id = 1, DepartmentId = DeptId, Status = ProjectStatus.Active };
        _projects.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _tasks.Setup(t => t.GetByProjectIdAsync(1, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new[]
            {
                new MPS.Domain.Entities.ProjectTask
                {
                    Id = 11, IsBatchParent = false, ProgressType = ProgressType.Milestone,
                    IsRequiredGate = true, Title = "QC mẫu sơn", Status = TaskItemStatus.Todo,
                },
            });

        var act = () => CompleteSut().Handle(new ProjectCompleteCommand(1, null), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
    }

    // ===== ProjectScopeGuard qua ProjectCreate =====

    private ProjectCreateCommandHandler CreateSut() =>
        new(_projects.Object, _departments.Object, _authorizer.Object, _currentUser.Object);

    private static ProjectCreateCommand CreateCmd() =>
        new("Đơn 500 khung", null, null, null, DeptId, Quantity: 500, ResponsibleUserId: TeamLeaderUserId);

    private void SetupValidTeamLeader() =>
        _departments.Setup(d => d.IsUserInDepartmentWithRoleAsync(
                TeamLeaderUserId, DeptId, SystemRoles.ProductionTeamLeaderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    [Fact]
    public async Task Create_Allowed_WhenDirectorHasReadAll()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(1);
        _currentUser.Setup(c => c.HasPermission(Permissions.ProjectReadAll)).Returns(true);
        SetupValidTeamLeader();
        _projects.Setup(p => p.AddAsync(It.IsAny<MPS.Domain.Entities.Project>(), It.IsAny<CancellationToken>())).ReturnsAsync(55);

        var id = await CreateSut().Handle(CreateCmd(), CancellationToken.None);

        id.Should().Be(55);
        // GĐ không cần check scope.
        _authorizer.Verify(a => a.IsManagerOfAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_Allowed_WhenManagerOfThatDepartment()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        _currentUser.Setup(c => c.HasPermission(Permissions.ProjectReadAll)).Returns(false);
        _authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupValidTeamLeader();
        _projects.Setup(p => p.AddAsync(It.IsAny<MPS.Domain.Entities.Project>(), It.IsAny<CancellationToken>())).ReturnsAsync(55);

        var id = await CreateSut().Handle(CreateCmd(), CancellationToken.None);

        id.Should().Be(55);
    }

    [Fact]
    public async Task Create_Throws403_WhenManagerOfDifferentDepartment()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(ManagerUserId);
        _currentUser.Setup(c => c.HasPermission(Permissions.ProjectReadAll)).Returns(false);
        _authorizer.Setup(a => a.IsManagerOfAsync(ManagerUserId, DeptId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => CreateSut().Handle(CreateCmd(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        _projects.Verify(p => p.AddAsync(It.IsAny<MPS.Domain.Entities.Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
