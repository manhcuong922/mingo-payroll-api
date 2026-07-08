using MPS.Domain.Entities;

namespace MPS.Application.Services;

public record ProjectProgress(
    int CompletionPercent,      // Task leaf -> Stage -> Project, weighted by Stage.PlannedWorkUnits
    int TimeElapsedPercent,     // elapsed time percent from StartDate to Deadline
    string ScheduleStatus,      // OnTrack | AtRisk | Late
    double PlannedValue,        // PV: expected work units by elapsed time
    double EarnedValue,         // EV: earned Stage.PlannedWorkUnits from StagePercent
    double SchedulePerformanceIndex,
    int TotalStages,
    int CompletedStages,
    IReadOnlyList<string> StagesWithoutTasks);

public interface IProgressService
{
    /// <summary>Computes project progress from leaf tasks through stages, with EV/PV/SPI on the same stage work-unit basis.</summary>
    ProjectProgress Compute(Project project, IReadOnlyList<ProjectTask> tasks, DateTime asOf);
}
