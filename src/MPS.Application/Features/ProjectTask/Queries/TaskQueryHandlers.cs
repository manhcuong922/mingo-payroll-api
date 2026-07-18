using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Application.Features.Project;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProjectTask.Queries;

public class TaskGetByIdQueryHandler : IRequestHandler<TaskGetByIdQuery, ProjectTaskDto?>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskGetByIdQueryHandler(
        IProjectTaskRepository tasks,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<ProjectTaskDto?> Handle(TaskGetByIdQuery request, CancellationToken ct)
    {
        var task = await _tasks.GetByIdWithGraphAsync(request.Id, ct);
        if (task is null) return null;

        await ProjectScopeGuard.EnsureCanReadTaskAsync(task, _tasks, _currentUser, _authorizer, ct);

        var includeChildren = _currentUser.HasPermission(Permissions.TaskReadAll)
            || _currentUser.HasPermission(Permissions.TaskReadDepartment)
            || _currentUser.HasPermission(Permissions.TaskReadFullDepartment);
        return TaskMapper.ToDto(task, includeChildren: includeChildren);
    }
}

public class TaskGetMyAssignedQueryHandler : IRequestHandler<TaskGetMyAssignedQuery, IReadOnlyList<ProjectTaskDto>>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly ICurrentUserService _currentUser;

    public TaskGetMyAssignedQueryHandler(IProjectTaskRepository tasks, ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ProjectTaskDto>> Handle(TaskGetMyAssignedQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Array.Empty<ProjectTaskDto>();

        var items = await _tasks.GetAssignedToUserAsync(userId.Value, ct, request.IncludeCancelled);
        return items.Select(t => TaskMapper.ToDto(t)).ToList();
    }
}

public class TaskGetByDepartmentQueryHandler
    : IRequestHandler<TaskGetByDepartmentQuery, IReadOnlyList<ProjectTaskDto>>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskGetByDepartmentQueryHandler(
        IProjectTaskRepository tasks,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ProjectTaskDto>> Handle(TaskGetByDepartmentQuery request, CancellationToken ct)
    {
        if (!_currentUser.HasPermission(Permissions.TaskReadAll))
        {
            var userId = _currentUser.UserId;
            if (userId is null || !await _authorizer.IsManagerOfAsync(userId.Value, request.DepartmentId, ct))
                throw new ForbiddenAccessException("Bạn không phải tổ trưởng của xưởng này.");
        }

        var items = await _tasks.GetByDepartmentAsync(request.DepartmentId, ct, request.IncludeCancelled);

        return items
            .Where(t => !t.IsConfidential || _currentUser.HasPermission(Permissions.TaskReadConfidential))
            .Select(t => TaskMapper.ToDto(t))
            .ToList();
    }
}

public class TaskGetByProjectQueryHandler
    : IRequestHandler<TaskGetByProjectQuery, IReadOnlyList<ProjectTaskDto>>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskGetByProjectQueryHandler(
        IProjectTaskRepository tasks,
        IProjectRepository projects,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _projects = projects;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ProjectTaskDto>> Handle(TaskGetByProjectQuery request, CancellationToken ct)
    {
        if (!_currentUser.HasPermission(Permissions.TaskReadAll))
        {
            var project = await _projects.GetByIdAsync(request.ProjectId, ct)
                ?? throw new NotFoundException($"Không tìm thấy đơn #{request.ProjectId}.");

            var userId = _currentUser.UserId;
            if (userId is null || project.DepartmentId is null
                || !await _authorizer.IsManagerOfAsync(userId.Value, project.DepartmentId.Value, ct))
            {
                throw new ForbiddenAccessException("Bạn không có quyền xem task của đơn này.");
            }
        }

        var items = await _tasks.GetByProjectIdAsync(request.ProjectId, ct, request.IncludeCancelled);

        return items
            .Where(t => !t.IsConfidential || _currentUser.HasPermission(Permissions.TaskReadConfidential))
            .Select(t => TaskMapper.ToDto(t))
            .ToList();
    }
}
