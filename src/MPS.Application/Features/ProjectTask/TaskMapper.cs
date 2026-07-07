using MPS.Application.DTOs;
using MPS.Domain.Entities;
using MPS.Domain.Enums;

namespace MPS.Application.Features.ProjectTask;

internal static class TaskMapper
{
    public static ProjectTaskDto ToDto(MPS.Domain.Entities.ProjectTask t, bool includeChildren = false)
    {
        var taskPercent = ResolveCompletionPercent(t);

        var assignees = t.Assignees
            .Select(a => new TaskAssigneeDto(a.UserId, a.User?.Username))
            .ToList();

        // Attachment lấy qua endpoint riêng (GET /api/Task/{id}/attachments) — polymorphic, không nhúng ở đây.
        var attachments = new List<ProductionAttachmentDto>();

        var children = includeChildren
            ? t.Children.OrderBy(c => c.Id).Select(c => ToDto(c, includeChildren: false)).ToList()
            : new List<ProjectTaskDto>();

        return new ProjectTaskDto(
            t.Id,
            t.Title,
            t.Description,
            t.Status.ToString(),
            t.Type.ToString(),
            t.ProgressType.ToString(),
            t.RequiresHandover,
            t.IsConfidential,
            t.Priority,
            t.StepOrder,
            t.Deadline,
            t.PlannedQuantity,
            t.CompletedQuantity,
            t.Weight,
            t.ProjectId,
            t.Project?.Title,
            t.DepartmentId,
            t.Department?.Name,
            t.ParentTaskId,
            t.ParentTask?.Title,
            assignees,
            t.CreatedByUserId,
            t.CreatedBy?.Username,
            t.IsBatchParent,
            t.AssignedPmUserId,
            taskPercent,
            t.WasLate,
            t.CompletedBy?.Username,
            t.CompletedAt,
            t.RowVersion is { Length: > 0 } ? Convert.ToBase64String(t.RowVersion) : string.Empty,
            attachments,
            children);
    }

    private static int ResolveCompletionPercent(MPS.Domain.Entities.ProjectTask t)
    {
        if (t.ProgressType == ProgressType.Milestone)
            return t.Status == TaskItemStatus.Done ? 100 : 0;

        if (t.PlannedQuantity <= 0)
            return t.Status == TaskItemStatus.Done ? 100 : 0;

        return (int)Math.Round(
            (double)Math.Min(t.CompletedQuantity, t.PlannedQuantity) / t.PlannedQuantity * 100);
    }
}
