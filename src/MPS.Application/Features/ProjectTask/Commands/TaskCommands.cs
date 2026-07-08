using MediatR;
using MPS.Domain.Enums;

namespace MPS.Application.Features.ProjectTask.Commands;

// ⭐ Tạo 1 task (1 người / nhóm). AssigneeUserIds nhiều phần tử = task nhóm (chung 1 Done).
// IsBatchParent=true → tạo công đoạn (tầng 2) thủ công, không qua ProcessTemplate: AssigneeUserIds phải rỗng.
public record TaskCreateCommand(
    int? ProjectId,
    int DepartmentId,
    int? ParentTaskId,
    string Title,
    string? Description,
    ProgressType ProgressType,
    int PlannedQuantity,
    int Weight,
    DateTime? Deadline,
    int? Priority,
    int StepOrder,
    IReadOnlyList<int> AssigneeUserIds,
    bool IsBatchParent = false,
    int? AssignedPmUserId = null) : IRequest<int>;  // chỉ có ý nghĩa khi IsBatchParent=true (gán PM ngay lúc tạo Công đoạn)

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

// ⭐ Bấm hoàn thành. Quá deadline vẫn Done nhưng gắn WasLate.
public record TaskCompleteCommand(
    int Id,
    string RowVersion) : IRequest<bool>;

public record TaskDeleteCommand(int Id) : IRequest<bool>;

// ⭐ Gán/đổi PM chịu trách nhiệm 1 Công đoạn (IsBatchParent=true). PM mới thừa kế toàn bộ Công đoạn +
// mọi task con hiện có ngay lập tức — quyền đi theo AssignedPmUserId hiện tại, không theo người tạo (plan mục 4.9).
public record TaskAssignPmCommand(int StageId, int PmUserId) : IRequest<bool>;

// ⭐ Pause/Resume/Cancel/Restore (plan mục 6.5). Với Công đoạn (IsBatchParent=true) lan truyền xuống
// mọi Task con của CHÍNH NÓ; với Task lá không lan truyền gì (không có tầng dưới).
public record TaskPauseCommand(int Id) : IRequest<bool>;
public record TaskResumeCommand(int Id) : IRequest<bool>;
public record TaskCancelCommand(int Id) : IRequest<bool>;
public record TaskRestoreCommand(int Id) : IRequest<bool>;
