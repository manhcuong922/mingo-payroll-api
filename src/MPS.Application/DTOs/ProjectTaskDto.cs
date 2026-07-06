namespace MPS.Application.DTOs;

public record TaskAssigneeDto(int UserId, string? UserName);

public record ProjectTaskDto(
    int Id,
    string Title,
    string? Description,
    string Status,
    string Type,
    string ProgressType,                // "Quantity" | "Milestone"
    bool RequiresHandover,
    bool IsConfidential,
    int? Priority,
    int StepOrder,
    DateTime? Deadline,
    int PlannedQuantity,
    int CompletedQuantity,
    int Weight,
    int? ProjectId,
    string? ProjectTitle,
    int? DepartmentId,
    string? DepartmentName,
    int? ParentTaskId,
    string? ParentTaskTitle,
    IReadOnlyList<TaskAssigneeDto> Assignees,   // 1 người / nhóm
    int CreatedByUserId,
    string? CreatedByName,
    bool IsBatchParent,
    int TaskCompletionPercent,          // Quantity: Completed/Planned×100; Milestone: Done?100:0
    bool WasLate,                       // Done sau deadline
    string? CompletedByName,
    DateTime? CompletedAt,
    string RowVersion,                  // base64 — gửi lại khi UpdateProgress/Complete (optimistic lock)
    IReadOnlyList<ProductionAttachmentDto> Attachments,
    List<ProjectTaskDto> Children);
