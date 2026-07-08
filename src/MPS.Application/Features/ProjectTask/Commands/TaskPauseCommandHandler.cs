using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project;
using MPS.Application.Services;
using MPS.Domain.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProjectTask.Commands;

// Pause: Công đoạn (Manager/GĐ) lan truyền xuống Task con của CHÍNH nó; Task lá (PM phụ trách/GĐ) không lan gì.
// Cho phép Pause cả khi đã Done (dùng để "khóa lại", plan mục 6.5).
public class TaskPauseCommandHandler : IRequestHandler<TaskPauseCommand, bool>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskPauseCommandHandler(
        IProjectTaskRepository tasks, IDepartmentManagerAuthorizer authorizer, ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(TaskPauseCommand request, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy task #{request.Id}.");

        await ProjectScopeGuard.EnsureCanWriteTaskLeafAsync(task, _tasks, _currentUser, _authorizer, ct);

        task.StatusBeforePause = task.Status;
        task.Status = Domain.Enums.TaskItemStatus.Paused;
        task.PausedByCascadeFromId = null; // Pause trực tiếp → tự Resume được (khác với con bị lan truyền)
        await _tasks.UpdateAsync(task, ct);

        if (task.IsBatchParent)
            await _tasks.CascadePauseChildrenAsync(task.Id, ct);

        return true;
    }
}
