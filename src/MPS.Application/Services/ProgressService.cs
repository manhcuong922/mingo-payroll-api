using MPS.Domain.Entities;
using MPS.Domain.Enums;

namespace MPS.Application.Services;

public sealed class ProgressService : IProgressService
{
    private const int AtRiskThresholdPercent = 10;   // công việc chậm hơn lịch quá ngưỡng này → AtRisk

    // v8 (docs/plans/plan.md): kiến trúc bottom-up qua 3 tầng, Gate/Checklist KHÔNG tham gia % ở tầng nào.
    //   Tầng 3 (Task lá Quantity) → CompletionPercent riêng, input thật (PM nhập) — BASE
    //   Tầng 2 (Công đoạn)        → StagePercent = SUM(CompletedQuantity con Quantity) / Stage.PlannedQuantity
    //   Tầng 1 (Đơn hàng)         → ProjectPercent = trung bình có trọng số StagePercent theo Stage.PlannedWorkUnits
    // Gate (ProgressType.Milestone) chỉ là điều kiện chặn Done/đóng đơn (IsRequiredGate) — không đóng góp
    // số liệu vào PlannedQuantity/CompletedQuantity/PlannedWorkUnits ở bất kỳ tầng nào.
    // EV/PV/SPI/BAC đọc trực tiếp Task con Quantity, nhưng loại Task con của công đoạn Cancelled — nếu
    // không, SPI có thể cộng cả sản lượng của công đoạn đã huỷ, lệch pha với ScheduleStatus/CompletionPercent
    // (vốn đã loại Cancelled ở tầng Công đoạn) cho cùng một đơn.
    public ProjectProgress Compute(Project project, IReadOnlyList<ProjectTask> tasks, DateTime asOf)
    {
        var stages = tasks.Where(t => t.IsBatchParent && IsCountable(t.Status)).ToList();
        var leaves = tasks.Where(t => !t.IsBatchParent && IsCountable(t.Status)).ToList();

        var cancelledStageIds = tasks
            .Where(t => t.IsBatchParent && t.Status == TaskItemStatus.Cancelled)
            .Select(t => t.Id)
            .ToHashSet();

        var (completionPercent, stagesWithoutTasks) = ComputeStageWeightedPercent(tasks, leaves);
        var (bac, ev) = SumQuantityTasks(leaves, cancelledStageIds);

        var timeElapsedPercent = ComputeTimeElapsedPercent(project, asOf);
        var pv = bac * timeElapsedPercent / 100.0;
        var spi = pv > 0 ? ev / pv : 0;

        var scheduleStatus = ResolveScheduleStatus(project, asOf, completionPercent, timeElapsedPercent);
        var completedStages = stages.Count(s => s.Status == TaskItemStatus.Done);

        return new ProjectProgress(
            completionPercent,
            timeElapsedPercent,
            scheduleStatus,
            Math.Round(pv, 2),
            Math.Round(ev, 2),
            Math.Round(spi, 2),
            stages.Count,
            completedStages,
            stagesWithoutTasks);
    }

    // Tầng 1 ← Tầng 2: ProjectPercent = trung bình có trọng số StagePercent theo PlannedWorkUnits của
    // CHÍNH công đoạn (không phải suy ra từ children) — công thức plan.md mục 3.3.
    private static (int CompletionPercent, IReadOnlyList<string> StagesWithoutTasks) ComputeStageWeightedPercent(
        IReadOnlyList<ProjectTask> tasks, IReadOnlyList<ProjectTask> leaves)
    {
        var allStages = tasks.Where(t => t.IsBatchParent).ToList();
        var stagesWithoutTasks = new List<string>();
        double totalWorkUnits = 0, earnedWorkUnits = 0;

        foreach (var stage in allStages)
        {
            if (stage.PlannedWorkUnits <= 0)
            {
                stagesWithoutTasks.Add($"{stage.Title} (id: {stage.Id})");
                continue; // PlannedWorkUnits=0 → "vô hình" trong mẫu số Tầng 1, chỉ cảnh báo, không cộng vào Σ
            }

            if (!IsCountable(stage.Status)) continue; // Paused/Cancelled — loại khỏi mẫu số Tầng 1

            var percent = ComputeStagePercent(stage, leaves);
            totalWorkUnits += stage.PlannedWorkUnits;
            earnedWorkUnits += stage.PlannedWorkUnits * percent / 100.0;
        }

        var completionPercent = totalWorkUnits > 0
            ? (int)Math.Round(earnedWorkUnits / totalWorkUnits * 100)
            : 0;
        return (completionPercent, stagesWithoutTasks);
    }

    // Tầng 2 ← Tầng 3: StagePercent = SUM(CompletedQuantity con Quantity) / Stage.PlannedQuantity.
    // Gate (Milestone) con KHÔNG tham gia — chỉ chặn Done qua TrySetParentDoneAsync, không tính vào %.
    private static int ComputeStagePercent(ProjectTask stage, IReadOnlyList<ProjectTask> leaves)
    {
        if (stage.PlannedQuantity <= 0) return 0;

        var completed = leaves
            .Where(child => child.ParentTaskId == stage.Id && child.ProgressType == ProgressType.Quantity)
            .Sum(child => Math.Min(child.CompletedQuantity, child.PlannedQuantity));

        return (int)Math.Round(Math.Min(completed, stage.PlannedQuantity) / (double)stage.PlannedQuantity * 100);
    }

    // EV/PV/SPI/BAC — đọc trực tiếp Task con Quantity (không qua trọng số Công đoạn, mục 6.3), nhưng bỏ
    // Task con của công đoạn đã Cancelled, để không cộng sản lượng của công đoạn đã huỷ vào SPI.
    private static (double Bac, double Ev) SumQuantityTasks(
        IReadOnlyList<ProjectTask> leaves, IReadOnlySet<int> cancelledStageIds)
    {
        double bac = 0, ev = 0;
        foreach (var t in leaves)
        {
            if (t.ProgressType != ProgressType.Quantity) continue;
            if (t.ParentTaskId is { } parentId && cancelledStageIds.Contains(parentId)) continue;
            bac += t.PlannedQuantity;
            ev += Math.Min(t.CompletedQuantity, t.PlannedQuantity);
        }
        return (bac, ev);
    }

    // Paused/Cancelled bị loại khỏi mẫu số tính tiến độ ở mọi tầng (plan mục 6.5) — coi như không tồn tại.
    private static bool IsCountable(TaskItemStatus status) =>
        status is not (TaskItemStatus.Paused or TaskItemStatus.Cancelled);

    private static int ComputeTimeElapsedPercent(Project project, DateTime asOf)
    {
        if (project.Deadline is not { } deadline) return 0;

        var start = project.StartDate.ToDateTime(TimeOnly.MinValue);
        var totalTicks = (deadline - start).TotalMinutes;
        if (totalTicks <= 0) return 100;

        var elapsed = (asOf - start).TotalMinutes;
        var pct = elapsed / totalTicks * 100;
        return (int)Math.Round(Math.Clamp(pct, 0, 100));
    }

    private static string ResolveScheduleStatus(
        Project project, DateTime asOf, int completionPercent, int timeElapsedPercent)
    {
        var overdue = project.Deadline is { } dl && asOf > dl;
        if (overdue && completionPercent < 100) return "Late";
        if (completionPercent < timeElapsedPercent - AtRiskThresholdPercent) return "AtRisk";
        return "OnTrack";
    }
}
