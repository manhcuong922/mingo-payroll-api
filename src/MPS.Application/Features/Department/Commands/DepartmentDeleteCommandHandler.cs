using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Department.Commands;

/// <summary>
/// "Giải tán xưởng" = SOFT-DELETE (set IsActive=false), KHÔNG hard-delete — xem Lưu ý kỹ thuật #5 &amp; #10.
/// Đơn Completed/Archived vẫn phải tham chiếu xưởng nên không được xóa cứng.
/// </summary>
public class DepartmentDeleteCommandHandler : IRequestHandler<DepartmentDeleteCommand, bool>
{
    private readonly IDepartmentRepository _repository;

    public DepartmentDeleteCommandHandler(IDepartmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DepartmentDeleteCommand request, CancellationToken cancellationToken)
    {
        // TODO(Task module): chặn 409 Conflict nếu còn đơn đang sản xuất —
        //   if (await _projectRepository.HasActiveByDepartmentAsync(request.Id, cancellationToken))
        //       throw new ConflictException("Không thể giải tán khi còn đơn đang sản xuất.");
        //   Chỉ khả dụng sau khi Task module (Project) tồn tại. Trước đó cho archive tự do.

        return await _repository.ArchiveAsync(request.Id, cancellationToken);
    }
}
