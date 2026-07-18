namespace MPS.Application.DTOs;

public record TaskAssigneeDto(int UserId, string? UserName);

public record ProjectTaskDto(
    int Id,
    string Title,
    string? Description,
    string Status,
    string Type,
    string ProgressType,                // "Quantity" (sản lượng) | "Milestone" (Gate/Checklist)
    bool IsConfidential,
    int? Priority,
    int StepOrder,
    DateTime? Deadline,
    int PlannedQuantity,                // Gate: luôn 0
    int CompletedQuantity,              // Gate: luôn 0
    int PlannedWorkUnits,               // trọng số/capacity ở MỌI node (container: mẫu số rollup; leaf: đóng góp)
    bool IsRequiredGate,                // chỉ có ý nghĩa khi ProgressType=Milestone (Gate)
    int PlannedHeadcount,               // số người kế hoạch của vị trí lá
    int AssignedHeadcount,              // số người đã gán (Assignees.Count)
    int? RequiredPositionId,            // chức danh bắt buộc để gán vào vị trí lá
    string? RequiredPositionName,
    double? DeadlineOffsetRatio,        // vị trí tương đối trong timeline đơn (0..1)
    int? ProjectId,
    string? ProjectTitle,
    int? DepartmentId,
    string? DepartmentName,
    int? ParentTaskId,
    string? ParentTaskTitle,
    IReadOnlyList<TaskAssigneeDto> Assignees,   // 1 người / nhóm
    int CreatedByUserId,
    string? CreatedByName,
    bool IsBatchParent,                  // node container (gộp con) ở bất kỳ tầng nào
    int TaskCompletionPercent,          // Quantity: Completed/Planned×100; Milestone: Done?100:0
    bool WasLate,                       // Done sau deadline
    string? CompletedByName,
    DateTime? CompletedAt,
    string RowVersion,                  // base64 — gửi lại khi UpdateProgress/Complete (optimistic lock)
    IReadOnlyList<ProductionAttachmentDto> Attachments,
    List<ProjectTaskDto> Children,
    int? PausedByCascadeFromId);        // null = Pause/Cancel trực tiếp (tự Resume/Restore được); khác null = bị lan truyền từ cha
