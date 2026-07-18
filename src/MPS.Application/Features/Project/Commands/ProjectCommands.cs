using MediatR;

namespace MPS.Application.Features.Project.Commands;

public record ProjectCreateCommand(
    string Title,
    string? Description,
    DateOnly? StartDate,
    DateTime? Deadline,
    int? DepartmentId,
    int Quantity,                 // hệ số scale sản lượng (Project.Quantity / Template.BaseQuantity)
    int ResponsibleUserId) : IRequest<int>;   // tổ trưởng sản xuất (role 6, cùng xưởng) — bắt buộc

// Planning sửa StartDate/Deadline → cascade co giãn deadline mọi task theo DeadlineOffsetRatio.
public record ProjectUpdateCommand(
    int Id,
    string Title,
    string? Description,
    DateOnly? StartDate,
    DateTime? Deadline,
    string? Status) : IRequest<bool>;

public record ProjectDeleteCommand(int Id) : IRequest<bool>;

public record ProjectGenerateTasksCommand(
    int ProjectId,
    int ProcessTemplateId) : IRequest<int>;   // trả về tổng số task đã sinh (full cây N tầng)

// ⭐ Đổi tổ trưởng sản xuất phụ trách đơn (Planning/GĐ). PmUserId phải role 6 + cùng xưởng.
public record ProjectSetResponsibleCommand(int ProjectId, int ResponsibleUserId) : IRequest<bool>;

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
