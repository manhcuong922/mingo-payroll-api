using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project;
using MPS.Application.Services;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using ProjectTaskEntity = MPS.Domain.Entities.ProjectTask;
using TaskAssignee = MPS.Domain.Entities.TaskAssignee;

namespace MPS.Application.Features.ProjectTask.Commands;

public class TaskCreateCommandHandler : IRequestHandler<TaskCreateCommand, int>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly IDepartmentRepository _departments;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskCreateCommandHandler(
        IProjectTaskRepository tasks,
        IProjectRepository projects,
        IDepartmentRepository departments,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _projects = projects;
        _departments = departments;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(TaskCreateCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        await ProjectScopeGuard.EnsureCanWriteTaskAsync(request.DepartmentId, _currentUser, _authorizer, ct);

        ValidateAssignment(request);

        var parent = await LoadAndValidateParentAsync(request, ct);
        await ValidateDeadlineScopeAsync(request, parent, ct);
        await ValidateQuantityScopeAsync(request, parent, ct);
        await ValidateAssigneesInDepartmentAsync(request, ct);

        var task = BuildTask(request, userId);
        return await _tasks.AddAsync(task, ct);
    }

    // Công đoạn (tầng 2) thủ công: không giao ai trực tiếp, không có cha. Task lá: cần ≥1 người.
    private static void ValidateAssignment(TaskCreateCommand request)
    {
        if (request.IsBatchParent)
        {
            if (request.AssigneeUserIds.Count > 0)
                throw new DomainValidationException("Công đoạn (task cha) không được giao người trực tiếp.");
            if (request.ParentTaskId is not null)
                throw new DomainValidationException("Công đoạn (task cha) không thể có cha khác — cây chỉ 3 tầng.");
        }
        else if (request.AssigneeUserIds.Count == 0)
        {
            throw new DomainValidationException("Task phải có ít nhất 1 người thực hiện.");
        }
    }

    private async Task<ProjectTaskEntity?> LoadAndValidateParentAsync(TaskCreateCommand request, CancellationToken ct)
    {
        if (request.ParentTaskId is not int parentId) return null;

        var parent = await _tasks.GetByIdAsync(parentId, ct)
            ?? throw new NotFoundException($"Không tìm thấy công đoạn cha #{parentId}.");

        if (!parent.IsBatchParent)
            throw new DomainValidationException("Task cha phải là công đoạn (IsBatchParent=true).");
        if (parent.DepartmentId != request.DepartmentId)
            throw new DomainValidationException("Task con phải cùng xưởng với công đoạn cha.");

        return parent;
    }

    // Deadline con không được vượt phạm vi cha (công đoạn) / đơn.
    private async Task ValidateDeadlineScopeAsync(TaskCreateCommand request, ProjectTaskEntity? parent, CancellationToken ct)
    {
        if (request.Deadline is not { } childDeadline) return;

        if (parent?.Deadline is { } parentDeadline && childDeadline > parentDeadline)
            throw new DomainValidationException("Deadline task con không được vượt quá deadline công đoạn cha.");

        if (request.ProjectId is not int projectId) return;

        var project = await _projects.GetByIdAsync(projectId, ct)
            ?? throw new NotFoundException($"Không tìm thấy đơn #{projectId}.");
        if (project.Deadline is { } projectDeadline && childDeadline > projectDeadline)
            throw new DomainValidationException("Deadline task không được vượt quá deadline đơn.");
    }

    // Tổng sản lượng các con không được vượt sản lượng công đoạn cha.
    private async Task ValidateQuantityScopeAsync(TaskCreateCommand request, ProjectTaskEntity? parent, CancellationToken ct)
    {
        if (request.IsBatchParent || request.ProgressType != ProgressType.Quantity || parent is null) return;

        var siblingsPlanned = await _tasks.SumChildrenPlannedQuantityAsync(parent.Id, excludeTaskId: null, ct);
        if (siblingsPlanned + request.PlannedQuantity > parent.PlannedQuantity)
            throw new DomainValidationException(
                $"Tổng sản lượng các task con ({siblingsPlanned + request.PlannedQuantity}) vượt quá sản lượng công đoạn cha ({parent.PlannedQuantity}).");
    }

    // Mọi assignee phải là công nhân thuộc đúng xưởng.
    private async Task ValidateAssigneesInDepartmentAsync(TaskCreateCommand request, CancellationToken ct)
    {
        if (request.AssigneeUserIds.Count == 0) return;

        var members = (await _departments.GetMemberUserIdsAsync(request.DepartmentId, ct)).ToHashSet();
        var outsiders = request.AssigneeUserIds.Where(id => !members.Contains(id)).ToList();
        if (outsiders.Count > 0)
            throw new DomainValidationException("Có người được giao không thuộc xưởng của task.");
    }

    private static ProjectTaskEntity BuildTask(TaskCreateCommand request, int userId)
    {
        var now = DateTime.UtcNow;
        return new ProjectTaskEntity
        {
            Title = request.Title.Trim(),
            Description = Normalize(request.Description),
            Type = TaskItemType.AdHoc,
            Status = TaskItemStatus.Todo,
            ProgressType = request.ProgressType,
            RequiresHandover = request.RequiresHandover,
            IsBatchParent = request.IsBatchParent,
            Priority = request.Priority,
            Deadline = request.Deadline,
            StepOrder = request.StepOrder,
            PlannedQuantity = request.ProgressType == ProgressType.Quantity ? request.PlannedQuantity : 0,
            Weight = request.ProgressType == ProgressType.Milestone ? request.Weight : 0,
            ProjectId = request.ProjectId,
            DepartmentId = request.DepartmentId,
            ParentTaskId = request.ParentTaskId,
            CreatedByUserId = userId,
            Assignees = request.AssigneeUserIds.Distinct()
                .Select(id => new TaskAssignee { UserId = id, AssignedAt = now })
                .ToList(),
        };
    }

    private static string? Normalize(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
