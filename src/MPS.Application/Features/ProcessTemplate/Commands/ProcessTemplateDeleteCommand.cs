using MediatR;

namespace MPS.Application.Features.ProcessTemplate.Commands;

// Xóa cứng bình thường (2026-07-03): đơn/task KHÔNG còn tham chiếu template → không cần chặn/soft-delete.
public record ProcessTemplateDeleteCommand(int Id) : IRequest<bool>;
