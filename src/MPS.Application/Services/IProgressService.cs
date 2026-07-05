using MPS.Domain.Entities;

namespace MPS.Application.Services;

public record ProjectProgress(
    int CompletionPercent,      // theo trọng số công việc (task Done)
    int TimeElapsedPercent,     // % thời gian đã trôi
    string ScheduleStatus,      // OnTrack | AtRisk | Late
    double PlannedValue,        // EV/PV — chỉ trên task Quantity
    double EarnedValue,
    double SchedulePerformanceIndex,
    int TotalStages,
    int CompletedStages);

public interface IProgressService
{
    /// <summary>Đo tiến độ LAI cho 1 đơn: trọng số công việc + % thời gian + EV/PV (subset task Quantity).</summary>
    ProjectProgress Compute(Project project, IReadOnlyList<ProjectTask> tasks, DateTime asOf);
}
