using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project.Queries;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.Project;

public sealed class ProjectQueryProgressTests
{
    private readonly Mock<IProjectRepository> _projects = new();
    private readonly Mock<IProjectTaskRepository> _tasks = new();
    private readonly Mock<IDepartmentManagerAuthorizer> _authorizer = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    [Fact]
    public async Task Paged_ReturnsComputedThreeLayerProgress()
    {
        _currentUser.Setup(c => c.HasPermission(Permissions.ProjectReadAll)).Returns(true);

        var project = new MPS.Domain.Entities.Project
        {
            Id = 10,
            Title = "Demo",
            Status = ProjectStatus.Active,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            Deadline = DateTime.UtcNow.AddDays(1),
        };
        var stage = new MPS.Domain.Entities.ProjectTask
        {
            Id = 100,
            ProjectId = 10,
            IsBatchParent = true,
            ProgressType = ProgressType.Quantity,
            PlannedQuantity = 200,
            PlannedWorkUnits = 200,
            Status = TaskItemStatus.InProgress,
        };
        var child1 = new MPS.Domain.Entities.ProjectTask
        {
            Id = 101,
            ProjectId = 10,
            ParentTaskId = 100,
            ProgressType = ProgressType.Quantity,
            PlannedQuantity = 100,
            CompletedQuantity = 50,
            Status = TaskItemStatus.InProgress,
        };
        var child2 = new MPS.Domain.Entities.ProjectTask
        {
            Id = 102,
            ProjectId = 10,
            ParentTaskId = 100,
            ProgressType = ProgressType.Quantity,
            PlannedQuantity = 100,
            CompletedQuantity = 100,
            Status = TaskItemStatus.Done,
        };

        _projects
            .Setup(p => p.GetPagedAsync(null, null, 1, 20, It.IsAny<CancellationToken>(), false, null))
            .ReturnsAsync((new[] { project }, 1));
        _tasks
            .Setup(t => t.GetByProjectIdsAsync(
                It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 10 })),
                It.IsAny<CancellationToken>(),
                false))
            .ReturnsAsync(new[] { stage, child1, child2 });

        var sut = new ProjectGetPagedQueryHandler(
            _projects.Object,
            _tasks.Object,
            new ProgressService(),
            _authorizer.Object,
            _currentUser.Object);

        var result = await sut.Handle(new ProjectGetPagedQuery(null, null, 1, 20), CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].CompletionPercent.Should().Be(75);
        result.Items[0].TotalStages.Should().Be(1);
        result.Items[0].CompletedStages.Should().Be(0);
    }
}
