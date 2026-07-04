using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Services;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProjectTask.Commands;

public class TaskUpdateCommandHandler : IRequestHandler<TaskUpdateCommand, bool>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentRepository _departments;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskUpdateCommandHandler(
        IProjectTaskRepository tasks,
        IDepartmentRepository departments,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _departments = departments;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(TaskUpdateCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        var task = await _tasks.GetByIdWithGraphAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy task #{request.Id}.");

        if (task.DepartmentId is null || !await _authorizer.IsManagerOfAsync(userId, task.DepartmentId.Value, ct))
            throw new ForbiddenAccessException("Bạn không phải quản lý của xưởng chứa task này.");

        task.Title = request.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        task.Deadline = request.Deadline;
        task.Priority = request.Priority;

        if (task.ProgressType == ProgressType.Quantity)
            task.PlannedQuantity = request.PlannedQuantity;
        else
            task.Weight = request.Weight;

        // Đổi assignee (nếu gửi). Validate thuộc xưởng.
        if (request.AssigneeUserIds is { } newAssignees)
        {
            if (newAssignees.Count == 0)
                throw new DomainValidationException("Task phải có ít nhất 1 người thực hiện.");

            var members = (await _departments.GetMemberUserIdsAsync(task.DepartmentId.Value, ct)).ToHashSet();
            if (newAssignees.Any(id => !members.Contains(id)))
                throw new DomainValidationException("Có người được giao không thuộc xưởng của task.");

            var now = DateTime.UtcNow;
            task.Assignees = newAssignees.Distinct()
                .Select(id => new TaskAssignee { TaskId = task.Id, UserId = id, AssignedAt = now })
                .ToList();
        }

        return await _tasks.UpdateAsync(task, ct);
    }
}
