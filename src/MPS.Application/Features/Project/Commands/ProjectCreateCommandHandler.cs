using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Services;
using MPS.Domain.Common;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Project.Commands;

public class ProjectCreateCommandHandler : IRequestHandler<ProjectCreateCommand, int>
{
    private readonly IProjectRepository _repository;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public ProjectCreateCommandHandler(
        IProjectRepository repository,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(ProjectCreateCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        // Scope v5: PM chỉ tạo đơn cho xưởng mình; GĐ (ProjectReadAll) tạo cho mọi xưởng.
        await ProjectScopeGuard.EnsureCanWriteAsync(request.DepartmentId, _currentUser, _authorizer, cancellationToken);

        var entity = new MPS.Domain.Entities.Project
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = ProjectStatus.Active,
            StartDate = request.StartDate ?? DateOnly.FromDateTime(VietnamClock.Now),
            Deadline = request.Deadline,
            Quantity = request.Quantity,
            DepartmentId = request.DepartmentId,
            CreatedByUserId = userId,
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
