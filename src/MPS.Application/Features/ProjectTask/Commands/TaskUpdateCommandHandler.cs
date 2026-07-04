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

public class TaskUpdateCommandHandler : IRequestHandler<TaskUpdateCommand, bool>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly IDepartmentRepository _departments;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskUpdateCommandHandler(
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

    public async Task<bool> Handle(TaskUpdateCommand request, CancellationToken ct)
    {
        var task = await _tasks.GetByIdWithGraphAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy task #{request.Id}.");

        await ProjectScopeGuard.EnsureCanWriteTaskAsync(task.DepartmentId, _currentUser, _authorizer, ct);

        await ValidateDeadlineScopeAsync(request, task, ct);
        await ValidateQuantityScopeAsync(request, task, ct);

        task.Title = request.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        task.Deadline = request.Deadline;
        task.Priority = request.Priority;

        if (task.ProgressType == ProgressType.Quantity)
            task.PlannedQuantity = request.PlannedQuantity;
        else
            task.Weight = request.Weight;

        await ApplyAssigneesAsync(request, task, ct);

        return await _tasks.UpdateAsync(task, ct);
    }

    // Deadline task không được vượt phạm vi cha (công đoạn, nếu là task con) / đơn.
    private async Task ValidateDeadlineScopeAsync(TaskUpdateCommand request, ProjectTaskEntity task, CancellationToken ct)
    {
        if (request.Deadline is not { } newDeadline) return;

        if (task.ParentTaskId is int parentId)
        {
            var parent = await _tasks.GetByIdAsync(parentId, ct);
            if (parent?.Deadline is { } parentDeadline && newDeadline > parentDeadline)
                throw new DomainValidationException("Deadline task con không được vượt quá deadline công đoạn cha.");
        }

        if (task.ProjectId is int projectId)
        {
            var project = await _projects.GetByIdAsync(projectId, ct);
            if (project?.Deadline is { } projectDeadline && newDeadline > projectDeadline)
                throw new DomainValidationException("Deadline task không được vượt quá deadline đơn.");
        }
    }

    // Tổng sản lượng các con (kể cả sau khi sửa) không được vượt sản lượng công đoạn cha.
    private async Task ValidateQuantityScopeAsync(TaskUpdateCommand request, ProjectTaskEntity task, CancellationToken ct)
    {
        if (task.IsBatchParent || task.ProgressType != ProgressType.Quantity || task.ParentTaskId is not int parentId)
            return;

        var parent = await _tasks.GetByIdAsync(parentId, ct);
        if (parent is null) return;

        var siblingsPlanned = await _tasks.SumChildrenPlannedQuantityAsync(parentId, excludeTaskId: task.Id, ct);
        if (siblingsPlanned + request.PlannedQuantity > parent.PlannedQuantity)
            throw new DomainValidationException(
                $"Tổng sản lượng các task con ({siblingsPlanned + request.PlannedQuantity}) vượt quá sản lượng công đoạn cha ({parent.PlannedQuantity}).");
    }

    // Đổi assignee (nếu gửi). Validate ≥1 người + thuộc xưởng.
    private async Task ApplyAssigneesAsync(TaskUpdateCommand request, ProjectTaskEntity task, CancellationToken ct)
    {
        if (request.AssigneeUserIds is not { } newAssignees) return;

        if (newAssignees.Count == 0)
            throw new DomainValidationException("Task phải có ít nhất 1 người thực hiện.");

        var members = (await _departments.GetMemberUserIdsAsync(task.DepartmentId!.Value, ct)).ToHashSet();
        if (newAssignees.Any(id => !members.Contains(id)))
            throw new DomainValidationException("Có người được giao không thuộc xưởng của task.");

        var now = DateTime.UtcNow;
        task.Assignees = newAssignees.Distinct()
            .Select(id => new TaskAssignee { TaskId = task.Id, UserId = id, AssignedAt = now })
            .ToList();
    }
}
