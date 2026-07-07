using MPS.Domain.Entities;

namespace MPS.Application.Services;

public record ProjectProgress(
    int CompletionPercent,      // theo trọng số công việc, tính qua Công đoạn (Tầng 2) — không đọc thẳng Task con
    int TimeElapsedPercent,     // % thời gian đã trôi
    string ScheduleStatus,      // OnTrack | AtRisk | Late
    double PlannedValue,        // EV/PV — chỉ trên task Quantity, đọc trực tiếp Task con (ngoại lệ có chủ đích — plan mục 6.3)
    double EarnedValue,
    double SchedulePerformanceIndex,
    int TotalStages,
    int CompletedStages,
    IReadOnlyList<string> StagesWithoutTasks);  // Công đoạn chưa có task con — "vô hình" trong CompletionPercent, cảnh báo cho người xem

public interface IProgressService
{
    /// <summary>Đo tiến độ LAI cho 1 đơn: trọng số công việc + % thời gian + EV/PV (subset task Quantity).</summary>
    ProjectProgress Compute(Project project, IReadOnlyList<ProjectTask> tasks, DateTime asOf);
}
