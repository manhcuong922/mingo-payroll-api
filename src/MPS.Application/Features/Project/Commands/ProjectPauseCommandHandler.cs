using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Services;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Project.Commands;

// Pause Đơn (Tầng 1) → lan truyền xuống TOÀN BỘ Công đoạn + Task con (plan mục 6.5).
// Cho phép cả khi đã Completed (dùng để "khóa lại" — vd khách hủy sau khi đã giao).
public class ProjectPauseCommandHandler : IRequestHandler<ProjectPauseCommand, bool>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public ProjectPauseCommandHandler(
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

    public async Task<bool> Handle(ProjectPauseCommand request, CancellationToken ct)
    {
        var project = await _projects.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy đơn #{request.Id}.");

        await ProjectScopeGuard.EnsureCanWriteAsync(project.DepartmentId, _currentUser, _authorizer, ct);

        project.StatusBeforePause = project.Status;
        project.Status = ProjectStatus.Paused;
        await _projects.UpdateAsync(project, ct);

        await _tasks.CascadePauseByProjectAsync(project.Id, ct);
        return true;
    }
}
