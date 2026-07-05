using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Project.Queries;

public class ProjectGetAllQueryHandler : IRequestHandler<ProjectGetAllQuery, IReadOnlyList<ProjectDto>>
{
    private readonly IProjectRepository _projects;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public ProjectGetAllQueryHandler(
        IProjectRepository projects,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _projects = projects;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ProjectDto>> Handle(ProjectGetAllQuery request, CancellationToken ct)
    {
        if (_currentUser.HasPermission(Permissions.ProjectReadAll))
        {
            var all = await _projects.GetAllAsync(ct);
            return all.Select(ProjectMapper.ToDto).ToList();
        }

        // ReadDepartment: tổ trưởng chỉ thấy đơn của xưởng mình.
        var userId = _currentUser.UserId;
        if (userId is null) return Array.Empty<ProjectDto>();

        var deptId = await _authorizer.GetManagedDepartmentIdAsync(userId.Value, ct);
        if (deptId is null) return Array.Empty<ProjectDto>();

        var items = await _projects.GetByDepartmentAsync(deptId.Value, ct);
        return items.Select(ProjectMapper.ToDto).ToList();
    }
}

public class ProjectGetPagedQueryHandler : IRequestHandler<ProjectGetPagedQuery, PagedResult<ProjectDto>>
{
    private readonly IProjectRepository _projects;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public ProjectGetPagedQueryHandler(
        IProjectRepository projects,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _projects = projects;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<ProjectDto>> Handle(ProjectGetPagedQuery request, CancellationToken ct)
    {
        int? departmentFilter = request.DepartmentId;

        if (!_currentUser.HasPermission(Permissions.ProjectReadAll))
        {
            var userId = _currentUser.UserId;
            var deptId = userId is null ? null : await _authorizer.GetManagedDepartmentIdAsync(userId.Value, ct);
            if (deptId is null)
                return new PagedResult<ProjectDto>(Array.Empty<ProjectDto>(), 0, request.Page, request.PageSize);

            departmentFilter = deptId;   // ép về xưởng mình
        }

        var (items, total) = await _projects.GetPagedAsync(
            request.Keyword, departmentFilter, request.Page, request.PageSize, ct);

        return new PagedResult<ProjectDto>(
            items.Select(ProjectMapper.ToDto).ToList(), total, request.Page, request.PageSize);
    }
}

public class ProjectGetByIdQueryHandler : IRequestHandler<ProjectGetByIdQuery, ProjectDto?>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectTaskRepository _tasks;
    private readonly IProgressService _progress;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public ProjectGetByIdQueryHandler(
        IProjectRepository projects,
        IProjectTaskRepository tasks,
        IProgressService progress,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _projects = projects;
        _tasks = tasks;
        _progress = progress;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<ProjectDto?> Handle(ProjectGetByIdQuery request, CancellationToken ct)
    {
        var project = await _projects.GetByIdAsync(request.Id, ct);
        if (project is null) return null;

        // Scope: ReadAll xem mọi đơn; ReadDepartment chỉ đơn xưởng mình.
        if (!_currentUser.HasPermission(Permissions.ProjectReadAll))
        {
            var userId = _currentUser.UserId;
            if (userId is null || project.DepartmentId is null
                || !await _authorizer.IsManagerOfAsync(userId.Value, project.DepartmentId.Value, ct))
            {
                throw new ForbiddenAccessException("Bạn không có quyền xem đơn này.");
            }
        }

        var tasks = await _tasks.GetByProjectIdAsync(request.Id, ct);
        return ProjectMapper.ToDtoWithProgress(project, tasks, _progress, DateTime.UtcNow);
    }
}
