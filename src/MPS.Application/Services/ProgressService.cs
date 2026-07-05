using MPS.Domain.Entities;
using MPS.Domain.Enums;

namespace MPS.Application.Services;

public sealed class ProgressService : IProgressService
{
    private const int AtRiskThresholdPercent = 10;   // công việc chậm hơn lịch quá ngưỡng này → AtRisk

    public ProjectProgress Compute(Project project, IReadOnlyList<ProjectTask> tasks, DateTime asOf)
    {
        // Chỉ tính trên task CON (loại task cha để tránh đếm đôi).
        var leaves = tasks.Where(t => !t.IsBatchParent).ToList();
        var stages = tasks.Where(t => t.IsBatchParent).ToList();

        double totalWeight = 0, earnedWeight = 0;
        double bac = 0, ev = 0;

        foreach (var t in leaves)
        {
            var weight = WeightOf(t);
            totalWeight += weight;
            earnedWeight += EarnedWeightOf(t);

            // EV/PV chỉ áp dụng cho task Quantity — đơn vị: SỐ LƯỢNG sản phẩm (không quy ra giờ công).
            if (t.ProgressType == ProgressType.Quantity)
            {
                bac += t.PlannedQuantity;
                ev += Math.Min(t.CompletedQuantity, t.PlannedQuantity);
            }
        }

        var completionPercent = totalWeight > 0
            ? (int)Math.Round(earnedWeight / totalWeight * 100)
            : 0;

        // --- Chiều thời gian ---
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
            completedStages);
    }

    // Trọng số đóng góp vào tiến độ đơn: Quantity → số lượng kế hoạch; Milestone → trọng số nhập tay.
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
