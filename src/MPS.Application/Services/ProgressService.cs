using MPS.Domain.Entities;
using MPS.Domain.Enums;

namespace MPS.Application.Services;

public sealed class ProgressService : IProgressService
{
    private const int AtRiskThresholdPercent = 10;   // công việc chậm hơn lịch quá ngưỡng này → AtRisk

    // v7 mục 6.2/6.3: kiến trúc bottom-up qua 3 tầng, không nhảy cấp.
    //   Tầng 3 (Task lá)   → CompletionPercent riêng, input thật (PM nhập) — BASE
    //   Tầng 2 (Công đoạn) → CompletionPercent = trọng số của Task con CỦA CHÍNH NÓ
    //   Tầng 1 (Đơn hàng)  → CompletionPercent = trọng số của Công đoạn (KHÔNG đọc thẳng Task con)
    // Ngoại lệ có chủ đích: EV/PV/SPI/BAC vẫn đọc trực tiếp Task con Quantity toàn Đơn (số liệu vật lý,
    // đi qua Công đoạn không thêm ý nghĩa) — chỉ CompletionPercent đi theo kiến trúc phân cấp mới.
    public ProjectProgress Compute(Project project, IReadOnlyList<ProjectTask> tasks, DateTime asOf)
    {
        var stages = tasks.Where(t => t.IsBatchParent && IsCountable(t.Status)).ToList();
        var leaves = tasks.Where(t => !t.IsBatchParent && IsCountable(t.Status)).ToList();

        var (completionPercent, stagesWithoutTasks) = ComputeStageWeightedPercent(tasks, leaves);
        var (bac, ev) = SumQuantityTasks(leaves);

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

    // Tầng 1 ← Tầng 2: CompletionPercent = trung bình có trọng số của từng Công đoạn (mục 6.3).
    private static (int CompletionPercent, IReadOnlyList<string> StagesWithoutTasks) ComputeStageWeightedPercent(
        IReadOnlyList<ProjectTask> tasks, IReadOnlyList<ProjectTask> leaves)
    {
        var allStages = tasks.Where(t => t.IsBatchParent).ToList();
        var stagesWithoutTasks = new List<string>();
        double totalStageWeight = 0, earnedStageWeight = 0;

        foreach (var stage in allStages)
        {
            var (weight, percent) = ComputeSingleStage(stage, leaves);
            if (weight <= 0)
            {
                stagesWithoutTasks.Add($"{stage.Title} (id: {stage.Id})");
                continue; // Weight=0 → "vô hình" trong mẫu số Tầng 1, chỉ cảnh báo, không cộng vào Σ
            }

            if (!IsCountable(stage.Status)) continue; // Paused/Cancelled — loại khỏi mẫu số Tầng 1

            totalStageWeight += weight;
            earnedStageWeight += weight * percent / 100.0;
        }

        var completionPercent = totalStageWeight > 0
            ? (int)Math.Round(earnedStageWeight / totalStageWeight * 100)
            : 0;
        return (completionPercent, stagesWithoutTasks);
    }

    // Tầng 2 ← Tầng 3: CompletionPercent của 1 Công đoạn = trọng số Task con CỦA CHÍNH NÓ.
    private static (double Weight, int CompletionPercent) ComputeSingleStage(
        ProjectTask stage, IReadOnlyList<ProjectTask> leaves)
    {
        double weight = 0, earned = 0;
        foreach (var child in leaves)
        {
            if (child.ParentTaskId != stage.Id) continue;
            weight += WeightOf(child);
            earned += EarnedWeightOf(child);
        }

        var percent = weight > 0 ? (int)Math.Round(earned / weight * 100) : 0;
        return (weight, percent);
    }

    // EV/PV/SPI/BAC — ngoại lệ có chủ đích, đọc trực tiếp Task con Quantity (không qua Công đoạn, mục 6.3).
    private static (double Bac, double Ev) SumQuantityTasks(IReadOnlyList<ProjectTask> leaves)
    {
        double bac = 0, ev = 0;
        foreach (var t in leaves)
        {
            if (t.ProgressType != ProgressType.Quantity) continue;
            bac += t.PlannedQuantity;
            ev += Math.Min(t.CompletedQuantity, t.PlannedQuantity);
        }
        return (bac, ev);
    }

    // Paused/Cancelled bị loại khỏi mẫu số tính tiến độ ở mọi tầng (plan mục 6.5) — coi như không tồn tại.
    private static bool IsCountable(TaskItemStatus status) =>
        status is not (TaskItemStatus.Paused or TaskItemStatus.Cancelled);

    // Trọng số đóng góp vào tiến độ Công đoạn: Quantity → số lượng kế hoạch; Milestone → trọng số nhập tay.
    private static double WeightOf(ProjectTask t) =>
        t.ProgressType == ProgressType.Quantity ? t.PlannedQuantity : t.Weight;

    private static double EarnedWeightOf(ProjectTask t)
    {
        if (t.ProgressType == ProgressType.Milestone)
            return t.Status == TaskItemStatus.Done ? t.Weight : 0;

        return Math.Min(t.CompletedQuantity, t.PlannedQuantity);
    }

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
