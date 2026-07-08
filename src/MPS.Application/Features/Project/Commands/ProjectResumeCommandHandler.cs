using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Services;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Project.Commands;

public class ProjectResumeCommandHandler : IRequestHandler<ProjectResumeCommand, bool>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public ProjectResumeCommandHandler(
        IProjectRepository projects,
        IProjectTaskRepository tasks,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _projects = projects;
        _tasks = tasks;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(ProjectResumeCommand request, CancellationToken ct)
    {
        var project = await _projects.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy đơn #{request.Id}.");

        if (project.Status != ProjectStatus.Paused)
            throw new DomainValidationException("Đơn không ở trạng thái Paused.");

        await ProjectScopeGuard.EnsureCanWriteAsync(project.DepartmentId, _currentUser, _authorizer, ct);

        project.Status = project.StatusBeforePause ?? ProjectStatus.Active;
        project.StatusBeforePause = null;
        await _projects.UpdateAsync(project, ct);

        await _tasks.CascadeResumeByProjectAsync(project.Id, ct);
        return true;
    }
}
