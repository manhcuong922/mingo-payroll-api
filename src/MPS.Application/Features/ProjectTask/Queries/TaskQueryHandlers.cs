using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
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

        var isAssignee = _currentUser.UserId is { } uid && task.Assignees.Any(a => a.UserId == uid);

        // Confidential: chỉ người tạo / người được assign / ReadConfidential.
        if (task.IsConfidential
            && !_currentUser.HasPermission(Permissions.TaskReadConfidential)
            && _currentUser.UserId != task.CreatedByUserId
            && !isAssignee)
        {
            throw new ForbiddenAccessException("Bạn không có quyền xem task nhạy cảm này.");
        }

        // ReadAll → mọi task. ReadDepartment → xưởng mình. ReadAssigned → chỉ task của mình.
        if (_currentUser.HasPermission(Permissions.TaskReadAll))
            return TaskMapper.ToDto(task, includeChildren: true);

        var userId = _currentUser.UserId;
        if (_currentUser.HasPermission(Permissions.TaskReadDepartment) && userId is not null
            && task.DepartmentId is not null
            && await _authorizer.IsManagerOfAsync(userId.Value, task.DepartmentId.Value, ct))
        {
            return TaskMapper.ToDto(task, includeChildren: true);
        }

        if (_currentUser.HasPermission(Permissions.TaskReadAssigned) && isAssignee)
            return TaskMapper.ToDto(task, includeChildren: false);

        throw new ForbiddenAccessException("Bạn không có quyền xem task này.");
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

        var items = await _tasks.GetAssignedToUserAsync(userId.Value, ct);
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

        var items = await _tasks.GetByDepartmentAsync(request.DepartmentId, ct);
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

        var items = await _tasks.GetByProjectIdAsync(request.ProjectId, ct);
        return items
            .Where(t => !t.IsConfidential || _currentUser.HasPermission(Permissions.TaskReadConfidential))
            .Select(t => TaskMapper.ToDto(t))
            .ToList();
    }
}
