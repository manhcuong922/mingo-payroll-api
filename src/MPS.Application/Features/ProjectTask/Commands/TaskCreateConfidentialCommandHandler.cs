using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Common;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProjectTask.Commands;

public class TaskCreateConfidentialCommandHandler : IRequestHandler<TaskCreateConfidentialCommand, int>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly ICurrentUserService _currentUser;

    public TaskCreateConfidentialCommandHandler(IProjectTaskRepository tasks, ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(TaskCreateConfidentialCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        if (request.AssigneeUserIds.Count == 0)
            throw new DomainValidationException("Task phải có ít nhất 1 người thực hiện.");

        var now = VietnamClock.Now;
        // Confidential: không gắn đơn/xưởng. Milestone (không sản lượng).
        var task = new MPS.Domain.Entities.ProjectTask
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Type = TaskItemType.InternalConfidential,
            Status = TaskItemStatus.Todo,
            ProgressType = ProgressType.Milestone,
            IsConfidential = true,
            Priority = request.Priority,
            Deadline = request.Deadline,
            ProjectId = null,
            DepartmentId = null,
            CreatedByUserId = userId,
            Assignees = request.AssigneeUserIds.Distinct()
                .Select(id => new TaskAssignee { UserId = id, AssignedAt = now })
                .ToList(),
        };

        return await _tasks.AddAsync(task, ct);
    }
}
