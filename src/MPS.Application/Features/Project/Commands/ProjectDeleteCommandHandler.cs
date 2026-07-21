using MediatR;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Project.Commands;

public class ProjectDeleteCommandHandler : IRequestHandler<ProjectDeleteCommand, bool>
{
    private readonly IProjectRepository _repository;

    public ProjectDeleteCommandHandler(IProjectRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(ProjectDeleteCommand request, CancellationToken cancellationToken)
    {
        var project = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (project is null) return false;

        // #19 — đơn đã đóng có cây task + lịch sử tiến độ + bảng đánh giá trỏ vào; xoá thẳng khiến
        // FK nổ ra HTTP 500. Đơn đã hoàn thành là dữ liệu lịch sử, phải giữ (muốn ẩn thì dùng Cancel).
        if (project.Status is ProjectStatus.Completed)
            throw new DomainValidationException(
                $"PROJECT_COMPLETED_NOT_DELETABLE: Không thể xoá đơn #{request.Id} đã hoàn thành — " +
                "đây là dữ liệu lịch sử (tiến độ, đánh giá, bảng lương tham chiếu).");

        return await _repository.DeleteAsync(request.Id, cancellationToken);
    }
}
