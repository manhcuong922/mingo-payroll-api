using MPS.Domain.Entities;
using MPS.Domain.Enums;

namespace MPS.Application.Services;

/// <summary>
/// Tiến độ đệ quy N tầng theo chuẩn WBS (refactor task-full-generation, mục 2).
/// NodePercent(node), bỏ qua con Paused/Cancelled:
///   - Work-leaf (Quantity, không con): PlannedQuantity&gt;0 ? min(Completed,Planned)/Planned : (Done?1:0)
///   - Container (có con): Σ(child.PlannedWorkUnits × NodePercent(child)) / node.PlannedWorkUnits, cap 1.0
///       → mẫu số = capacity RIÊNG của node ⇒ chưa chia hết capacity thì % &lt; 100 (under-allocation mọi tầng).
///   - Gate (Milestone): KHÔNG tham gia % (chỉ chặn Done).
/// ProjectPercent = earned/BAC ở gốc ảo bọc node top-level, weight theo PlannedWorkUnits.
/// EV = earned gốc; BAC = Σ PlannedWorkUnits node top-level; PV = BAC × timeElapsed; SPI = EV/PV.
/// </summary>
public sealed class ProgressService : IProgressService
{
    private const int AtRiskThresholdPercent = 10;

    public ProjectProgress Compute(Project project, IReadOnlyList<ProjectTask> tasks, DateTime asOf)
    {
        // 0 = sentinel cho "root" (ProjectTask.Id luôn > 0) — Dictionary<int?,_> ném lỗi với key null.
        var childrenByParent = tasks
            .GroupBy(t => t.ParentTaskId ?? 0)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Node top-level = task không có cha TRONG tập đang xét (ParentTaskId null hoặc cha ngoài tập).
        var ids = tasks.Select(t => t.Id).ToHashSet();
        var topLevel = tasks
            .Where(t => t.ParentTaskId is null || !ids.Contains(t.ParentTaskId.Value))
            .Where(t => IsCountable(t.Status) && !IsGate(t))
            .ToList();

        double earned = 0, bac = 0;
        var stagesWithoutTasks = new List<string>();
        var memo = new Dictionary<int, double>();

        foreach (var node in topLevel)
        {
            var capacity = node.PlannedWorkUnits;
            if (capacity <= 0)
            {
                stagesWithoutTasks.Add($"{node.Title} (id: {node.Id})");
                continue; // PlannedWorkUnits=0 → "vô hình" trong mẫu số gốc, chỉ cảnh báo.
            }

            earned += capacity * NodePercent(node, childrenByParent, memo);
            bac += capacity;
        }

        var completionPercent = bac > 0 ? (int)Math.Round(earned / bac * 100) : 0;

        var timeElapsedPercent = ComputeTimeElapsedPercent(project, asOf);
        var pv = bac * timeElapsedPercent / 100.0;
        var ev = earned;
        var spi = pv > 0 ? ev / pv : 0;

        var scheduleStatus = ResolveScheduleStatus(project, asOf, completionPercent, timeElapsedPercent);

        var totalStages = topLevel.Count;
        var completedStages = topLevel.Count(n => n.Status == TaskItemStatus.Done);

        return new ProjectProgress(
            completionPercent,
            timeElapsedPercent,
            scheduleStatus,
            Math.Round(pv, 2),
            Math.Round(ev, 2),
            Math.Round(spi, 2),
            totalStages,
            completedStages,
            stagesWithoutTasks);
    }

    // Tỷ lệ hoàn thành 0..1 của 1 node. Memo hoá theo Id chống tính lại + chống trùng đếm.
    private static double NodePercent(
        ProjectTask node,
        IReadOnlyDictionary<int, List<ProjectTask>> childrenByParent,
        IDictionary<int, double> memo)
    {
        if (memo.TryGetValue(node.Id, out var cached)) return cached;

        double result;
        var children = childrenByParent.TryGetValue(node.Id, out var cs)
            ? cs.Where(c => IsCountable(c.Status)).ToList()
            : new List<ProjectTask>();

        var effectiveChildren = children.Where(c => !IsGate(c)).ToList();

        if (effectiveChildren.Count > 0)
        {
            // Container: Σ(child.workunits × childPercent) / capacity riêng node (cap 1.0).
            double childEarned = 0, childWorkUnits = 0;
            foreach (var child in effectiveChildren)
            {
                childEarned += child.PlannedWorkUnits * NodePercent(child, childrenByParent, memo);
                childWorkUnits += child.PlannedWorkUnits;
            }

            var denom = node.PlannedWorkUnits > 0 ? node.PlannedWorkUnits : childWorkUnits;
            result = denom > 0 ? Math.Min(childEarned / denom, 1.0) : 0;
        }
        else if (IsGate(node))
        {
            result = 0; // Gate không tính % (đề phòng gọi trực tiếp).
        }
        else
        {
            // Work-leaf Quantity.
            result = node.PlannedQuantity > 0
                ? Math.Min(node.CompletedQuantity, node.PlannedQuantity) / (double)node.PlannedQuantity
                : (node.Status == TaskItemStatus.Done ? 1.0 : 0.0);
        }

        memo[node.Id] = result;
        return result;
    }

    private static bool IsGate(ProjectTask t) => t.ProgressType == ProgressType.Milestone;

    // Paused/Cancelled bị loại khỏi mẫu số mọi tầng (coi như không tồn tại).
    private static bool IsCountable(TaskItemStatus status) =>
        status is not (TaskItemStatus.Paused or TaskItemStatus.Cancelled);

    private static int ComputeTimeElapsedPercent(Project project, DateTime asOf)
    {
        if (project.Deadline is not { } deadline) return 0;

        var start = project.StartDate.ToDateTime(TimeOnly.MinValue);
        var totalMinutes = (deadline - start).TotalMinutes;
        if (totalMinutes <= 0) return 100;

        var elapsed = (asOf - start).TotalMinutes;
        var pct = elapsed / totalMinutes * 100;
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
