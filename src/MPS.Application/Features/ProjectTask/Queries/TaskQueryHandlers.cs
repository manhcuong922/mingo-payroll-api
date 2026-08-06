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

        var isManager = _currentUser.HasPermission(Permissions.TaskReadAll)
            || _currentUser.HasPermission(Permissions.TaskReadDepartment)
            || _currentUser.HasPermission(Permissions.TaskReadFullDepartment);

        if (isManager)
        {
            return TaskMapper.ToDto(task, includeChildren: true);
        }

        // Công nhân (Task.Read.Assigned): mở node container → chỉ thấy children thuộc "nhánh của mình"
        // (assigned leaves + tổ tiên trong project). Leaf người khác trong cùng công đoạn KHÔNG hiện.
        if (task.IsBatchParent
            && _currentUser.HasPermission(Permissions.TaskReadAssigned)
            && _currentUser.UserId is int workerId
            && task.ProjectId is int projectId)
        {
            var siblings = await _tasks.GetByProjectIdAsync(projectId, ct, includeCancelled: true);
            var visible = ProjectScopeGuard.ComputeVisibleTaskIdsForAssignedUser(siblings, workerId);
            task.Children = task.Children.Where(c => visible.Contains(c.Id)).ToList();
            return TaskMapper.ToDto(task, includeChildren: true);
        }

        // Leaf worker được gán trực tiếp → không có children để render (leaf), map thẳng.
        return TaskMapper.ToDto(task, includeChildren: false);
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
        var isFullReader = _currentUser.HasPermission(Permissions.TaskReadAll);
        var isManagerOfProjectDepartment = false;

        if (!isFullReader)
        {
            var project = await _projects.GetByIdAsync(request.ProjectId, ct)
                ?? throw new NotFoundException($"Không tìm thấy đơn #{request.ProjectId}.");

            var userId = _currentUser.UserId;
            isManagerOfProjectDepartment = userId is not null && project.DepartmentId is not null
                && await _authorizer.IsManagerOfAsync(userId.Value, project.DepartmentId.Value, ct);

            // Công nhân (Task.Read.Assigned) — pass gate nếu có ít nhất 1 task được gán trong đơn;
            // filter cụ thể (assigned leaves ∪ tổ tiên) chạy sau khi load flat list.
            var isAssignedWorker = _currentUser.HasPermission(Permissions.TaskReadAssigned)
                && userId is int workerId
                && await _tasks.HasAssignedTaskInProjectAsync(workerId, request.ProjectId, ct);

            if (!isManagerOfProjectDepartment && !isAssignedWorker)
                throw new ForbiddenAccessException("Bạn không có quyền xem task của đơn này.");
        }

        var items = await _tasks.GetByProjectIdAsync(request.ProjectId, ct, request.IncludeCancelled);

        var canSeeConfidential = _currentUser.HasPermission(Permissions.TaskReadConfidential);
        var visibleItems = items.Where(t => !t.IsConfidential || canSeeConfidential);

        // Quản lý xưởng đơn / GĐ giữ nguyên hành vi trọn cây. Công nhân → chỉ leaves được gán + tổ tiên.
        if (!isFullReader && !isManagerOfProjectDepartment)
        {
            if (_currentUser.UserId is not int workerId)
                return Array.Empty<ProjectTaskDto>();

            var visibleIds = ProjectScopeGuard.ComputeVisibleTaskIdsForAssignedUser(items, workerId);
            visibleItems = visibleItems.Where(t => visibleIds.Contains(t.Id));
        }

        return visibleItems.Select(t => TaskMapper.ToDto(t)).ToList();
    }
}
