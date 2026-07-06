using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Services;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Project.Commands;

public class ProjectUpdateCommandHandler : IRequestHandler<ProjectUpdateCommand, bool>
{
    private readonly IProjectRepository _repository;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public ProjectUpdateCommandHandler(
        IProjectRepository repository,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(ProjectUpdateCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy đơn #{request.Id}.");

        // Scope v5: PM chỉ sửa đơn xưởng mình; GĐ mọi xưởng.
        await ProjectScopeGuard.EnsureCanWriteAsync(entity.DepartmentId, _currentUser, _authorizer, cancellationToken);

        entity.Title = request.Title.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Deadline = request.Deadline;
        entity.Quantity = request.Quantity;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<ProjectStatus>(request.Status, ignoreCase: true, out var status))
                throw new DomainValidationException($"Trạng thái đơn không hợp lệ: {request.Status}.");
            entity.Status = status;
        }

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
