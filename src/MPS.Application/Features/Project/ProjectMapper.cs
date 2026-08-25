using MPS.Application.DTOs;
using MPS.Application.Services;

namespace MPS.Application.Features.Project;

internal static class ProjectMapper
{
    // Bản gọn (list/paged) — không có cây task nên tiến độ = 0.
    public static ProjectDto ToDto(MPS.Domain.Entities.Project p) =>
        new(
            p.Id,
            p.Title,
            p.Description,
            p.Status.ToString(),
            p.StartDate,
            p.Deadline,
            p.Quantity,
            p.DepartmentId,
            p.Department?.Name,
            p.ResponsibleUserId,
            p.ResponsibleUser?.FullName,
            p.CreatedByUserId,
            p.CreatedBy?.FullName,
            p.CompletedBy?.FullName,
            p.CompletedAt,
            p.CompletionNote,
            CompletionPercent: 0,
            TimeElapsedPercent: 0,
            ScheduleStatus: "OnTrack",
            PlannedValue: 0,
            EarnedValue: 0,
            SchedulePerformanceIndex: 0,
            TotalStages: 0,
            CompletedStages: 0,
            StagesWithoutTasks: Array.Empty<string>());

    // Bản đầy đủ (GetById) — có cây task + tiến độ LAI.
    public static ProjectDto ToDtoWithProgress(
        MPS.Domain.Entities.Project p,
        IReadOnlyList<MPS.Domain.Entities.ProjectTask> tasks,
        IProgressService progress,
        DateTime asOf)
    {
        var pr = progress.Compute(p, tasks, asOf);

        return new ProjectDto(
            p.Id,
            p.Title,
            p.Description,
            p.Status.ToString(),
            p.StartDate,
            p.Deadline,
            p.Quantity,
            p.DepartmentId,
            p.Department?.Name,
            p.ResponsibleUserId,
            p.ResponsibleUser?.FullName,
            p.CreatedByUserId,
            p.CreatedBy?.FullName,
            p.CompletedBy?.FullName,
            p.CompletedAt,
            p.CompletionNote,
            pr.CompletionPercent,
            pr.TimeElapsedPercent,
            pr.ScheduleStatus,
            pr.PlannedValue,
            pr.EarnedValue,
            pr.SchedulePerformanceIndex,
            pr.TotalStages,
            pr.CompletedStages,
            pr.StagesWithoutTasks);
    }
}
