using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Project.Commands;

// ⭐ Đổi tổ trưởng sản xuất phụ trách đơn (refactor R/S). Quyền tiến độ chuyển ngay theo ResponsibleUserId mới.
public class ProjectSetResponsibleCommandHandler : IRequestHandler<ProjectSetResponsibleCommand, bool>
{
    private readonly IProjectRepository _projects;
    private readonly IDepartmentRepository _departments;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public ProjectSetResponsibleCommandHandler(
        IProjectRepository projects,
        IDepartmentRepository departments,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _projects = projects;
        _departments = departments;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(ProjectSetResponsibleCommand request, CancellationToken ct)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException($"Không tìm thấy đơn #{request.ProjectId}.");

        await ProjectScopeGuard.EnsureCanWriteAsync(project.DepartmentId, _currentUser, _authorizer, ct);

        if (project.DepartmentId is not int deptId)
            throw new DomainValidationException("Đơn phải gán xưởng để chọn tổ trưởng sản xuất.");

        var isValid = await _departments.IsUserInDepartmentWithRoleAsync(
            request.ResponsibleUserId, deptId, SystemRoles.ProductionTeamLeaderId, ct);
        if (!isValid)
            throw new DomainValidationException("Tổ trưởng sản xuất phải có role Tổ trưởng và cùng xưởng với đơn.");

        project.ResponsibleUserId = request.ResponsibleUserId;
        return await _projects.UpdateAsync(project, ct);
    }
}
