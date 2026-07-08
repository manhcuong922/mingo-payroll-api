using MediatR;
using MPS.Application.Services;

namespace MPS.Application.Features.Project.Commands;

public record ProjectCreateCommand(
    string Title,
    string? Description,
    DateOnly? StartDate,
    DateTime? Deadline,
    int? DepartmentId) : IRequest<int>;

public record ProjectUpdateCommand(
    int Id,
    string Title,
    string? Description,
    DateTime? Deadline,
    string? Status) : IRequest<bool>;

public record ProjectDeleteCommand(int Id) : IRequest<bool>;

public record ProjectGenerateTasksCommand(
    int ProjectId,
    int ProcessTemplateId,
    IReadOnlyList<GenerateStepOverride>? Overrides) : IRequest<int>;   // trả về số task cha đã sinh

// ⭐ Đóng đơn thủ công (v5): khách xác nhận đủ hàng → PM bấm hoàn thành + ghi chú rút kinh nghiệm.
// CompletedAt: tuỳ chọn — cho phép chỉnh ngày hoàn thành thủ công. Không truyền → VietnamClock.Now.
public record ProjectCompleteCommand(
    int Id,
    string? CompletionNote,
    DateTime? CompletedAt = null) : IRequest<bool>;

// ⭐ Pause/Resume/Cancel/Restore (plan mục 6.5) — lan truyền xuống TOÀN BỘ Công đoạn + Task con của Đơn.
public record ProjectPauseCommand(int Id) : IRequest<bool>;
public record ProjectResumeCommand(int Id) : IRequest<bool>;
public record ProjectCancelCommand(int Id) : IRequest<bool>;
public record ProjectRestoreCommand(int Id) : IRequest<bool>;
