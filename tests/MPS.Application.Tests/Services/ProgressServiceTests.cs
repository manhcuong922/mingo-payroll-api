using FluentAssertions;
using MPS.Application.Services;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using Xunit;

namespace MPS.Application.Tests.Services;

public sealed class ProgressServiceTests
{
    private readonly ProgressService _sut = new();

    private static Project Project(DateOnly start, DateTime? deadline) =>
        new() { Id = 1, StartDate = start, Deadline = deadline, Quantity = 100 };

    private static ProjectTask Quantity(int planned, int completed, bool done = false, int? parentId = null) =>
        new()
        {
            ProgressType = ProgressType.Quantity,
            IsBatchParent = false,
            PlannedQuantity = planned,
            CompletedQuantity = completed,
            Status = done ? TaskItemStatus.Done : TaskItemStatus.InProgress,
            ParentTaskId = parentId,
        };

    private static ProjectTask Milestone(int weight, bool done, int? parentId = null) =>
        new()
        {
            ProgressType = ProgressType.Milestone,
            IsBatchParent = false,
            Weight = weight,
            Status = done ? TaskItemStatus.Done : TaskItemStatus.Todo,
            ParentTaskId = parentId,
        };

    private static ProjectTask Stage(int id) =>
        new() { Id = id, IsBatchParent = true, Status = TaskItemStatus.InProgress };

    [Fact]
    public void Compute_CompletionByWeight_QuantityTasks()
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5));
        var project = Project(start, DateTime.UtcNow.AddDays(5));
        // Kiến trúc bottom-up (v7 mục 6.2): Tầng 1 chỉ đọc qua Công đoạn (Tầng 2) — task lá phải có cha.
        // Trọng số = plannedQuantity. Task1: 100 (đã 50 → earned 50). Task2: 100 (đã 100 → earned 100).
        var stage = Stage(1);
        var tasks = new List<ProjectTask> { stage, Quantity(100, 50, parentId: 1), Quantity(100, 100, parentId: 1) };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        // Công đoạn: earned 150 / total 200 = 75% → Đơn (1 công đoạn, trọng số 200) = 75%
        result.CompletionPercent.Should().Be(75);
    }

    [Fact]
    public void Compute_ExcludesBatchParents_ToAvoidDoubleCount()
    {
        var project = Project(DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow.AddDays(10));
        var stage = Stage(1);
        var child = Quantity(100, 50, parentId: 1);
        var tasks = new List<ProjectTask> { stage, child };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        // Chỉ tính child: 50/100 = 50%
        result.CompletionPercent.Should().Be(50);
    }

    [Fact]
    public void Compute_MilestoneTask_CountsWeightOnlyWhenDone()
    {
        var project = Project(DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow.AddDays(10));
        var stage = Stage(1);
        var tasks = new List<ProjectTask> { stage, Milestone(10, done: false, parentId: 1), Milestone(10, done: true, parentId: 1) };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        // Total weight 20, earned 10 (chỉ task done) = 50%
        result.CompletionPercent.Should().Be(50);
    }

    [Fact]
    public void Compute_ScheduleStatus_Late_WhenPastDeadlineAndNotDone()
    {
        var project = Project(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), DateTime.UtcNow.AddDays(-1));
        var tasks = new List<ProjectTask> { Quantity(100, 50) };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        result.ScheduleStatus.Should().Be("Late");
    }

    [Fact]
    public void Compute_ScheduleStatus_AtRisk_WhenWorkLagsTime()
    {
        // Đã trôi ~90% thời gian nhưng chỉ xong 10% việc → AtRisk.
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-9));
        var project = Project(start, DateTime.UtcNow.AddDays(1));
        var tasks = new List<ProjectTask> { Quantity(100, 10) };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        result.ScheduleStatus.Should().Be("AtRisk");
    }

    [Fact]
    public void Compute_ReturnsZeroPv_WhenNoDeadline()
    {
        var project = Project(DateOnly.FromDateTime(DateTime.UtcNow), deadline: null);
        var tasks = new List<ProjectTask> { Quantity(100, 50) };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        result.PlannedValue.Should().Be(0);
        result.SchedulePerformanceIndex.Should().Be(0);
        result.TimeElapsedPercent.Should().Be(0);
    }

    [Fact]
    public void Compute_CountsStages_FromBatchParents()
    {
        var project = Project(DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow.AddDays(10));
        var stageDone = Quantity(100, 100, done: true);
        stageDone.IsBatchParent = true;
        var stageOpen = Quantity(100, 0);
        stageOpen.IsBatchParent = true;
        var tasks = new List<ProjectTask> { stageDone, stageOpen, Quantity(100, 50) };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        result.TotalStages.Should().Be(2);
        result.CompletedStages.Should().Be(1);
    }
}
