using MediatR;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Department.Commands;

/// <summary>
/// "Giải tán xưởng" = SOFT-DELETE (set IsActive=false), KHÔNG hard-delete — xem Lưu ý kỹ thuật #5 &amp; #10.
/// Đơn Completed/Archived vẫn phải tham chiếu xưởng nên không được xóa cứng.
/// Chặn khi còn đơn đang sản xuất (Project.Status == Active).
/// </summary>
public class DepartmentDeleteCommandHandler : IRequestHandler<DepartmentDeleteCommand, bool>
{
    private readonly IDepartmentRepository _repository;
    private readonly IProjectRepository _projectRepository;

    public DepartmentDeleteCommandHandler(
        IDepartmentRepository repository,
        IProjectRepository projectRepository)
    {
        _repository = repository;
        _projectRepository = projectRepository;
    }

    public async Task<bool> Handle(DepartmentDeleteCommand request, CancellationToken cancellationToken)
    {
        // Lưu ý #5: chặn giải tán khi còn đơn đang sản xuất (đơn Completed/Archived thì cho phép).
        if (await _projectRepository.HasActiveByDepartmentAsync(request.Id, cancellationToken))
            throw new ConflictException("Không thể giải tán xưởng khi còn đơn đang sản xuất.");

        return await _repository.ArchiveAsync(request.Id, cancellationToken);
    }
}
