using MediatR;
using MPS.Application.Services;

namespace MPS.Application.Features.Project.Commands;

public record ProjectCreateCommand(
    string Title,
    string? Description,
    DateOnly? StartDate,
    DateTime? Deadline,
    int Quantity,
    int? DepartmentId) : IRequest<int>;

public record ProjectUpdateCommand(
    int Id,
    string Title,
    string? Description,
    DateTime? Deadline,
    string? Status,
    int Quantity) : IRequest<bool>;

public record ProjectDeleteCommand(int Id) : IRequest<bool>;

public record ProjectGenerateTasksCommand(
    int ProjectId,
    int ProcessTemplateId,
    IReadOnlyList<GenerateStepOverride>? Overrides) : IRequest<int>;   // trả về số task cha đã sinh

// ⭐ Đóng đơn thủ công (v5): khách xác nhận đủ hàng → PM bấm hoàn thành + ghi chú rút kinh nghiệm.
public record ProjectCompleteCommand(
    int Id,
    string? CompletionNote) : IRequest<bool>;
