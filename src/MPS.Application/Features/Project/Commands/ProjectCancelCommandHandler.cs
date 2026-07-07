using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Services;
using MPS.Domain.Common;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Project.Commands;

// Cancel = soft-delete Đơn → lan truyền xuống TOÀN BỘ Công đoạn + Task con (plan mục 6.5).
public class ProjectCancelCommandHandler : IRequestHandler<ProjectCancelCommand, bool>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public ProjectCancelCommandHandler(
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

    public async Task<bool> Handle(ProjectCancelCommand request, CancellationToken ct)
    {
        var project = await _projects.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy đơn #{request.Id}.");

        await ProjectScopeGuard.EnsureCanWriteAsync(project.DepartmentId, _currentUser, _authorizer, ct);

        var now = VietnamClock.Now;
        project.StatusBeforePause = project.Status;
        project.Status = ProjectStatus.Cancelled;
        project.DeletedAt = now;
        await _projects.UpdateAsync(project, ct);

        await _tasks.CascadeCancelByProjectAsync(project.Id, now, ct);
        return true;
    }
}
