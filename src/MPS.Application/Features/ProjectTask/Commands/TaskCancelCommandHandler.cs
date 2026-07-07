using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project;
using MPS.Application.Services;
using MPS.Domain.Common;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProjectTask.Commands;

// Cancel = soft-delete (Status=Cancelled + DeletedAt). Cho phép cả khi đã Done (plan mục 6.5).
public class TaskCancelCommandHandler : IRequestHandler<TaskCancelCommand, bool>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskCancelCommandHandler(
        IProjectTaskRepository tasks, IDepartmentManagerAuthorizer authorizer, ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(TaskCancelCommand request, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy task #{request.Id}.");

        await ProjectScopeGuard.EnsureCanWriteTaskLeafAsync(task, _tasks, _currentUser, _authorizer, ct);

        var now = VietnamClock.Now;
        task.StatusBeforePause = task.Status;
        task.Status = TaskItemStatus.Cancelled;
        task.PausedByCascadeFromId = null; // Cancel trực tiếp → tự Restore được
        task.DeletedAt = now;
        await _tasks.UpdateAsync(task, ct);

        if (task.IsBatchParent)
            await _tasks.CascadeCancelChildrenAsync(task.Id, now, ct);

        return true;
    }
}
