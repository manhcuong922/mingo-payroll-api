using MediatR;
using MPS.Domain.Enums;

namespace MPS.Application.Features.ProjectTask.Commands;

// ⭐ Tạo 1 task (1 người / nhóm). AssigneeUserIds nhiều phần tử = task nhóm (chung 1 Done).
public record TaskCreateCommand(
    int? ProjectId,
    int DepartmentId,
    int? ParentTaskId,
    string Title,
    string? Description,
    ProgressType ProgressType,
    int PlannedQuantity,
    int Weight,
    bool RequiresHandover,
    DateTime? Deadline,
    int? Priority,
    int StepOrder,
    IReadOnlyList<int> AssigneeUserIds) : IRequest<int>;

public record TaskCreateConfidentialCommand(
    string Title,
    string? Description,
    IReadOnlyList<int> AssigneeUserIds,
    DateTime? Deadline,
    int? Priority) : IRequest<int>;

public record TaskUpdateCommand(
    int Id,
    string Title,
    string? Description,
    DateTime? Deadline,
    int? Priority,
    int PlannedQuantity,
    int Weight,
    IReadOnlyList<int>? AssigneeUserIds) : IRequest<bool>;   // null = giữ nguyên assignee

// ⭐ Cập nhật sản lượng (chỉ task Quantity). RowVersion base64 để optimistic-lock.
public record TaskUpdateProgressCommand(
    int Id,
    int CompletedQuantity,
    string RowVersion) : IRequest<bool>;

// ⭐ Bấm hoàn thành. Quá deadline vẫn Done nhưng gắn WasLate. Task bàn giao bắt buộc attachment.
public record TaskCompleteCommand(
    int Id,
    string RowVersion) : IRequest<bool>;

public record TaskDeleteCommand(int Id) : IRequest<bool>;
