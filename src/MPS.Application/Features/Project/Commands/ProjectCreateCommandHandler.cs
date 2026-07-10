using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Services;
using MPS.Domain.Common;
using MPS.Domain.Constants;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Project.Commands;

public class ProjectCreateCommandHandler : IRequestHandler<ProjectCreateCommand, int>
{
    private readonly IProjectRepository _repository;
    private readonly IDepartmentRepository _departments;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public ProjectCreateCommandHandler(
        IProjectRepository repository,
        IDepartmentRepository departments,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _departments = departments;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(ProjectCreateCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        // Scope: Planning Manager chỉ tạo đơn cho xưởng mình; GĐ (ProjectReadAll) tạo cho mọi xưởng.
        await ProjectScopeGuard.EnsureCanWriteAsync(request.DepartmentId, _currentUser, _authorizer, cancellationToken);

        await ValidateResponsibleUserAsync(request, cancellationToken);

        var entity = new MPS.Domain.Entities.Project
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = ProjectStatus.Active,
            StartDate = request.StartDate ?? DateOnly.FromDateTime(VietnamClock.Now),
            Deadline = request.Deadline,
            Quantity = request.Quantity,
            DepartmentId = request.DepartmentId,
            ResponsibleUserId = request.ResponsibleUserId,
            CreatedByUserId = userId,
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }

    // Tổ trưởng sản xuất phải role Production Team Leader (6) + cùng xưởng với đơn (refactor R/S).
    private async Task ValidateResponsibleUserAsync(ProjectCreateCommand request, CancellationToken ct)
    {
        if (request.DepartmentId is not int deptId)
            throw new DomainValidationException("Đơn phải gán xưởng để chọn tổ trưởng sản xuất.");

        var isValid = await _departments.IsUserInDepartmentWithRoleAsync(
            request.ResponsibleUserId, deptId, SystemRoles.ProductionTeamLeaderId, ct);
        if (!isValid)
            throw new DomainValidationException("Tổ trưởng sản xuất phải có role Tổ trưởng và cùng xưởng với đơn.");
    }
}
