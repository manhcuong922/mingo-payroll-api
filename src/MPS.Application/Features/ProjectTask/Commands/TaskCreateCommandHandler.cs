using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Services;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProjectTask.Commands;

public class TaskCreateCommandHandler : IRequestHandler<TaskCreateCommand, int>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentRepository _departments;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskCreateCommandHandler(
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

    public async Task<int> Handle(TaskCreateCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        if (!await _authorizer.IsManagerOfAsync(userId, request.DepartmentId, ct))
            throw new ForbiddenAccessException("Bạn không phải quản lý của xưởng này.");

        if (request.AssigneeUserIds.Count == 0)
            throw new DomainValidationException("Task phải có ít nhất 1 người thực hiện.");

        // Mọi assignee phải là công nhân thuộc đúng xưởng.
        var members = (await _departments.GetMemberUserIdsAsync(request.DepartmentId, ct)).ToHashSet();
        var outsiders = request.AssigneeUserIds.Where(id => !members.Contains(id)).ToList();
        if (outsiders.Count > 0)
            throw new DomainValidationException("Có người được giao không thuộc xưởng của task.");

        var now = DateTime.UtcNow;
        var task = new MPS.Domain.Entities.ProjectTask
        {
            Title = request.Title.Trim(),
            Description = Normalize(request.Description),
            Type = TaskItemType.AdHoc,
            Status = TaskItemStatus.Todo,
            ProgressType = request.ProgressType,
            RequiresHandover = request.RequiresHandover,
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

        return await _tasks.AddAsync(task, ct);
    }

    private static string? Normalize(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
