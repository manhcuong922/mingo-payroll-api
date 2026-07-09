using FluentAssertions;
using MPS.Application.Services;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using Xunit;

namespace MPS.Application.Tests.Services;

// Refactor task-full-generation: tiến độ đệ quy N tầng theo WBS.
//   - Work-leaf (Quantity): min(Completed,Planned)/Planned.
//   - Container: Σ(child.PlannedWorkUnits × childPct) / node.PlannedWorkUnits (capacity riêng, cap 1.0)
//       ⇒ chưa chia hết capacity ⇒ % < 100 (under-allocation ở mọi tầng).
//   - Gate (Milestone): KHÔNG tham gia %.
//   - Paused/Cancelled: loại khỏi mẫu số ở mọi tầng.
//   - BAC = Σ PlannedWorkUnits node top-level; EV = earned; PV = BAC × time; SPI = EV/PV.
public sealed class ProgressServiceTests
{
    private readonly ProgressService _sut = new();

    private static Project Project(DateOnly start, DateTime? deadline) =>
        new() { Id = 1, StartDate = start, Deadline = deadline, Quantity = 100 };

    // Container = node có con (nhận diện theo cấu trúc). PlannedWorkUnits = capacity/mẫu số riêng.
    private static ProjectTask Container(int id, int plannedWorkUnits, int? parentId = null,
        TaskItemStatus status = TaskItemStatus.InProgress) =>
        new()
        {
            Id = id, ProgressType = ProgressType.Quantity, IsBatchParent = true,
            PlannedQuantity = 0, PlannedWorkUnits = plannedWorkUnits,
            Status = status, ParentTaskId = parentId,
        };

    // Work-leaf Quantity: PlannedQuantity là mẫu số %, PlannedWorkUnits là trọng số khi rollup lên cha.
    private static ProjectTask Leaf(int id, int planned, int completed, int workUnits,
        int? parentId = null, bool done = false, TaskItemStatus? status = null) =>
        new()
        {
            Id = id, ProgressType = ProgressType.Quantity, IsBatchParent = false,
            PlannedQuantity = planned, CompletedQuantity = completed, PlannedWorkUnits = workUnits,
            Status = status ?? (done ? TaskItemStatus.Done : TaskItemStatus.InProgress),
            ParentTaskId = parentId,
        };

    private static ProjectTask Gate(int id, bool done, int? parentId = null) =>
        new()
        {
            Id = id, ProgressType = ProgressType.Milestone, IsRequiredGate = true,
            Status = done ? TaskItemStatus.Done : TaskItemStatus.Todo, ParentTaskId = parentId,
        };

    [Fact]
    public void Compute_Container_WeightsChildrenByWorkUnits()
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5));
        var project = Project(start, DateTime.UtcNow.AddDays(5));
        // Container capacity 200; 2 con lấp đầy (100+100). Con A 50% (50/100), con B 100% (100/100).
        // earned = 100×0.5 + 100×1.0 = 150 ; denom = 200 ⇒ 75%.
        var tasks = new List<ProjectTask>
        {
            Container(1, plannedWorkUnits: 200),
            Leaf(2, planned: 100, completed: 50, workUnits: 100, parentId: 1),
            Leaf(3, planned: 100, completed: 100, workUnits: 100, parentId: 1, done: true),
        };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        result.CompletionPercent.Should().Be(75);
    }

    [Fact]
    public void Compute_UnderAllocation_CapacityIsDenominator()
    {
        // Capacity 500 nhưng chỉ 300 WorkUnits được giao, dù con đó xong 100% ⇒ 300/500 = 60%.
        var project = Project(DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow.AddDays(10));
        var tasks = new List<ProjectTask>
        {
            Container(1, plannedWorkUnits: 500),
            Leaf(2, planned: 300, completed: 300, workUnits: 300, parentId: 1, done: true),
        };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        result.CompletionPercent.Should().Be(60);
    }

    [Fact]
    public void Compute_Container_NotDoubleCounted()
    {
        var project = Project(DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow.AddDays(10));
        // Chỉ con Quantity đóng góp; container không tự cộng thêm capacity của chính nó.
        var tasks = new List<ProjectTask>
        {
            Container(1, plannedWorkUnits: 100),
            Leaf(2, planned: 100, completed: 50, workUnits: 100, parentId: 1),
        };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        result.CompletionPercent.Should().Be(50);
    }

    [Fact]
    public void Compute_Gate_DoesNotAffectCompletionPercent()
    {
        // Gate (Milestone) không tham gia % dù Done hay chưa — chỉ chặn Done container.
        var project = Project(DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow.AddDays(10));
        var tasks = new List<ProjectTask>
        {
            Container(1, plannedWorkUnits: 100),
            Leaf(2, planned: 100, completed: 50, workUnits: 100, parentId: 1),
            Gate(3, done: false, parentId: 1),
            Gate(4, done: true, parentId: 1),
        };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        result.CompletionPercent.Should().Be(50);
    }

    [Fact]
    public void Compute_Recurses_MultiTier()
    {
        var project = Project(DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow.AddDays(10));
        // Gốc(200) → [Nhánh A(100) → leaf 100% , Nhánh B(100) → leaf 50%]. A=1.0, B=0.5.
        // Gốc = (100×1 + 100×0.5)/200 = 0.75.
        var tasks = new List<ProjectTask>
        {
            Container(1, plannedWorkUnits: 200),
            Container(2, plannedWorkUnits: 100, parentId: 1),
            Container(3, plannedWorkUnits: 100, parentId: 1),
            Leaf(4, planned: 100, completed: 100, workUnits: 100, parentId: 2, done: true),
            Leaf(5, planned: 100, completed: 50, workUnits: 100, parentId: 3),
        };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        result.CompletionPercent.Should().Be(75);
    }

    [Fact]
    public void Compute_ScheduleStatus_Late_WhenPastDeadlineAndNotDone()
    {
        var project = Project(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), DateTime.UtcNow.AddDays(-1));
        var tasks = new List<ProjectTask> { Leaf(1, planned: 100, completed: 50, workUnits: 100) };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        result.ScheduleStatus.Should().Be("Late");
    }

    [Fact]
    public void Compute_ScheduleStatus_AtRisk_WhenWorkLagsTime()
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-9));
        var project = Project(start, DateTime.UtcNow.AddDays(1));
        var tasks = new List<ProjectTask> { Leaf(1, planned: 100, completed: 10, workUnits: 100) };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        result.ScheduleStatus.Should().Be("AtRisk");
    }

    [Fact]
    public void Compute_ReturnsZeroPv_WhenNoDeadline()
    {
        var project = Project(DateOnly.FromDateTime(DateTime.UtcNow), deadline: null);
        var tasks = new List<ProjectTask> { Leaf(1, planned: 100, completed: 50, workUnits: 100) };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        result.PlannedValue.Should().Be(0);
        result.SchedulePerformanceIndex.Should().Be(0);
        result.TimeElapsedPercent.Should().Be(0);
    }

    [Fact]
    public void Compute_ExcludesCancelledTopLevel_FromBacAndEv()
    {
        // Node Cancelled (đã xong 100/100 trước khi huỷ) không được kéo BAC/EV lên gây SPI ảo.
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-9));
        var project = Project(start, DateTime.UtcNow.AddDays(1)); // ~90% thời gian đã trôi
        var tasks = new List<ProjectTask>
        {
            Leaf(1, planned: 100, completed: 100, workUnits: 100, status: TaskItemStatus.Cancelled),
            Leaf(2, planned: 100, completed: 10, workUnits: 100),
        };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        // Chỉ node còn hiệu lực: BAC=100, EV=10 ⇒ SPI thấp, khớp AtRisk.
        result.EarnedValue.Should().Be(10);
        result.SchedulePerformanceIndex.Should().BeLessThan(1);
        result.ScheduleStatus.Should().Be("AtRisk");
    }

    [Fact]
    public void Compute_Spi_UsesWorkUnits_AndExcludesPausedBranch()
    {
        var project = Project(new DateOnly(2026, 1, 1), new DateTime(2026, 1, 21));
        // 4 công đoạn top-level, capacity 300 mỗi cái.
        //  - cut: container Done, 2 leaf 150+150 xong ⇒ 100% (earned 300).
        //  - pausedJoin: Paused ⇒ loại khỏi BAC (cả con của nó).
        //  - paint, pack: leaf trống ⇒ 0%.
        // BAC = 300×3 = 900 (loại paused); EV = 300; time = 75% ⇒ PV=675, SPI≈0.44, completion 33%.
        var tasks = new List<ProjectTask>
        {
            Container(1, plannedWorkUnits: 300, status: TaskItemStatus.Done),
            Container(2, plannedWorkUnits: 300, status: TaskItemStatus.Paused),
            Leaf(3, planned: 300, completed: 0, workUnits: 300),
            Leaf(4, planned: 300, completed: 0, workUnits: 300),
            Leaf(5, planned: 150, completed: 150, workUnits: 150, parentId: 1, done: true),
            Leaf(6, planned: 150, completed: 150, workUnits: 150, parentId: 1, done: true),
            Leaf(7, planned: 150, completed: 60, workUnits: 150, parentId: 2),
            Leaf(8, planned: 150, completed: 60, workUnits: 150, parentId: 2),
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
    public void Compute_CountsTopLevelNodes_AsStages()
    {
        var project = Project(DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow.AddDays(10));
        var tasks = new List<ProjectTask>
        {
            Leaf(1, planned: 100, completed: 100, workUnits: 100, done: true),
            Leaf(2, planned: 100, completed: 0, workUnits: 100),
        };

        var result = _sut.Compute(project, tasks, DateTime.UtcNow);

        result.TotalStages.Should().Be(2);
        result.CompletedStages.Should().Be(1);
    }
}
