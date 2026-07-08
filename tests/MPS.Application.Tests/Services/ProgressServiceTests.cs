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

    private static ProjectTask Gate(bool done, bool isRequired = true, int? parentId = null) =>
        new()
        {
            ProgressType = ProgressType.Milestone,
            IsBatchParent = false,
            IsRequiredGate = isRequired,
            Status = done ? TaskItemStatus.Done : TaskItemStatus.Todo,
            ParentTaskId = parentId,
        };

    // plannedQuantity = sản lượng kế hoạch của CHÍNH công đoạn (mẫu số StagePercent).
    // plannedWorkUnits = khối lượng công việc — trọng số của công đoạn khi tổng hợp lên Đơn.
    private static ProjectTask Stage(int id, int plannedQuantity = 0, int plannedWorkUnits = 0) =>
        new()
        {
            Id = id, IsBatchParent = true, Status = TaskItemStatus.InProgress,
            ProgressType = ProgressType.Quantity,
            PlannedQuantity = plannedQuantity, PlannedWorkUnits = plannedWorkUnits,
        };

    [Fact]
    public void Compute_CompletionByWeight_QuantityTasks()
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5));
        var project = Project(start, DateTime.UtcNow.AddDays(5));
        // v8 (docs/plans/plan.md): StagePercent = SUM(CompletedQuantity con Quantity) / Stage.PlannedQuantity.
        // Stage kế hoạch 200 (100+100), 2 con lần lượt Done 50 và 100 → earned 150/200 = 75%.
        // 1 công đoạn duy nhất, PlannedWorkUnits=200 (bất kỳ giá trị dương nào) → ProjectPercent = StagePercent = 75%.
        var stage = Stage(1, plannedQuantity: 200, plannedWorkUnits: 200);
        var tasks = new List<ProjectTask> { stage, Quantity(100, 50, parentId: 1), Quantity(100, 100, parentId: 1) };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        result.CompletionPercent.Should().Be(75);
    }

    [Fact]
    public void Compute_ExcludesBatchParents_ToAvoidDoubleCount()
    {
        var project = Project(DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow.AddDays(10));
        var stage = Stage(1, plannedQuantity: 100, plannedWorkUnits: 100);
        var child = Quantity(100, 50, parentId: 1);
        var tasks = new List<ProjectTask> { stage, child };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        // StagePercent = 50/100 = 50% → ProjectPercent (1 stage) = 50%
        result.CompletionPercent.Should().Be(50);
    }

    [Fact]
    public void Compute_Gate_DoesNotAffectCompletionPercent()
    {
        // v8: Gate/Checklist (Milestone) KHÔNG tham gia % dù Done hay chưa — chỉ chặn TrySetParentDoneAsync,
        // không đóng góp số liệu vào StagePercent/ProjectPercent.
        var project = Project(DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow.AddDays(10));
        var stage = Stage(1, plannedQuantity: 100, plannedWorkUnits: 100);
        var tasks = new List<ProjectTask>
        {
            stage, Quantity(100, 50, parentId: 1), Gate(done: false, parentId: 1), Gate(done: true, parentId: 1),
        };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        // Chỉ con Quantity tham gia: 50/100 = 50%, bất kể Gate Done hay chưa.
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
    public void Compute_Spi_ExcludesQuantityTasks_OfCancelledStage()
    {
        // Bug thực tế: 1 đơn có Stage A (Cancelled, đã hoàn thành 100/100 trước khi huỷ) và Stage B
        // (InProgress, ì ạch 10/100). Nếu SPI cộng cả Stage A đã huỷ, BAC/EV bị đẩy lên sai và SPI
        // có thể báo "Vượt tiến độ" trong khi ScheduleStatus báo "AtRisk" cho cùng một đơn — mâu thuẫn
        // hiển thị trên UI. Sau fix: Task con của Stage Cancelled không được tính vào BAC/EV.
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-9));
        var project = Project(start, DateTime.UtcNow.AddDays(1)); // ~90% thời gian đã trôi
        var cancelledStage = new ProjectTask
        {
            Id = 1, IsBatchParent = true, Status = TaskItemStatus.Cancelled,
            ProgressType = ProgressType.Quantity, PlannedQuantity = 100, PlannedWorkUnits = 100,
        };
        var activeStage = Stage(2, plannedQuantity: 100, plannedWorkUnits: 100);
        var tasks = new List<ProjectTask>
        {
            cancelledStage, activeStage,
            Quantity(100, 100, parentId: 1), // con của Stage đã huỷ — không được tính vào SPI
            Quantity(100, 10, parentId: 2),  // Stage còn hiệu lực, ì ạch
        };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        // BAC/EV chỉ tính trên Stage 2: BAC=100, EV=10 → SPI thấp, khớp với ScheduleStatus AtRisk.
        result.SchedulePerformanceIndex.Should().BeLessThan(1);
        result.ScheduleStatus.Should().Be("AtRisk");
    }

    [Fact]
    public void Compute_Spi_UsesStageWorkUnits_NotOnlyExistingLeafTasks()
    {
        var project = Project(new DateOnly(2026, 1, 1), new DateTime(2026, 1, 21));
        var cut = Stage(1, plannedQuantity: 300, plannedWorkUnits: 300);
        cut.Status = TaskItemStatus.Done;
        var pausedJoin = Stage(2, plannedQuantity: 300, plannedWorkUnits: 300);
        pausedJoin.Status = TaskItemStatus.Paused;
        var paint = Stage(3, plannedQuantity: 300, plannedWorkUnits: 300);
        var pack = Stage(4, plannedQuantity: 300, plannedWorkUnits: 300);
        var tasks = new List<ProjectTask>
        {
            cut,
            pausedJoin,
            paint,
            pack,
            Quantity(150, 150, done: true, parentId: 1),
            Quantity(150, 150, done: true, parentId: 1),
            Quantity(150, 60, parentId: 2),
            Quantity(150, 60, parentId: 2),
        };

        var result = _sut.Compute(project, tasks, new DateTime(2026, 1, 16));

        result.CompletionPercent.Should().Be(33);
        result.TimeElapsedPercent.Should().Be(75);
        result.PlannedValue.Should().Be(675);
        result.EarnedValue.Should().Be(300);
        result.SchedulePerformanceIndex.Should().BeApproximately(0.44, 0.01);
        result.ScheduleStatus.Should().Be("AtRisk");
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
