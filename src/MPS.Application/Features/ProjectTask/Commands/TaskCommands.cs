using MediatR;
using MPS.Domain.Enums;

namespace MPS.Application.Features.ProjectTask.Commands;

// ⭐ Tạo 1 task (1 người / nhóm) HOẶC 1 Gate/Checklist (ProgressType=Milestone, task lá không giao sản lượng).
// AssigneeUserIds nhiều phần tử = task nhóm (chung 1 Done).
// IsBatchParent=true → tạo công đoạn (tầng 2) thủ công, không qua ProcessTemplate: AssigneeUserIds phải rỗng.
public record TaskCreateCommand(
    int? ProjectId,
    int DepartmentId,
    int? ParentTaskId,
    string Title,
    string? Description,
    ProgressType ProgressType,
    int PlannedQuantity,
    int PlannedWorkUnits,          // trọng số/capacity ở mọi node
    bool IsRequiredGate,           // chỉ có ý nghĩa khi ProgressType=Milestone (Gate) — mặc định true khi tạo qua UI
    DateTime? Deadline,
    int? Priority,
    int StepOrder,
    IReadOnlyList<int> AssigneeUserIds,
    bool IsBatchParent = false,
    int PlannedHeadcount = 0,      // số người kế hoạch của vị trí lá
    int? RequiredPositionId = null // chức danh bắt buộc để gán vào vị trí lá
    ) : IRequest<int>;

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
    int PlannedWorkUnits,          // chỉ có ý nghĩa khi task là công đoạn (IsBatchParent=true)
    bool IsRequiredGate,           // chỉ có ý nghĩa khi task là Gate (ProgressType=Milestone)
    IReadOnlyList<int>? AssigneeUserIds) : IRequest<bool>;   // null = giữ nguyên assignee

// ⭐ Cập nhật sản lượng (chỉ task Quantity). RowVersion base64 để optimistic-lock.
public record TaskUpdateProgressCommand(
    int Id,
    int CompletedQuantity,
    string RowVersion) : IRequest<bool>;

// ⭐ Bấm hoàn thành. Quá deadline vẫn Done nhưng gắn WasLate.
// CompletedAt: tuỳ chọn — cho phép chỉnh ngày hoàn thành thủ công (vd ghi nhận ngày thực tế đã xong trước đó).
// Không truyền → mặc định thời điểm hiện tại (VietnamClock.Now).
public record TaskCompleteCommand(
    int Id,
    string RowVersion,
    DateTime? CompletedAt = null) : IRequest<bool>;

public record TaskDeleteCommand(int Id) : IRequest<bool>;

// ⭐ Pause/Resume/Cancel/Restore (plan mục 6.5). Với Công đoạn (IsBatchParent=true) lan truyền xuống
// mọi Task con của CHÍNH NÓ; với Task lá không lan truyền gì (không có tầng dưới).
public record TaskPauseCommand(int Id) : IRequest<bool>;
public record TaskResumeCommand(int Id) : IRequest<bool>;
public record TaskCancelCommand(int Id) : IRequest<bool>;
public record TaskRestoreCommand(int Id) : IRequest<bool>;
