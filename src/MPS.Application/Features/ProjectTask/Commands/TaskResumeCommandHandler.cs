using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project;
using MPS.Application.Services;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProjectTask.Commands;

// Resume: CHỈ từ đúng tầng đã ra lệnh Pause trực tiếp (PausedByCascadeFromId == null).
// Task bị Pause DO lan truyền từ Công đoạn cha không tự Resume được — phải Resume từ Công đoạn cha (plan mục 6.5).
public class TaskResumeCommandHandler : IRequestHandler<TaskResumeCommand, bool>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskResumeCommandHandler(
        IProjectTaskRepository tasks, IDepartmentManagerAuthorizer authorizer, ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(TaskResumeCommand request, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy task #{request.Id}.");

        if (task.Status != TaskItemStatus.Paused)
            throw new DomainValidationException("Task không ở trạng thái Paused.");
        if (task.PausedByCascadeFromId is not null)
            throw new DomainValidationException("Task đang bị khóa bởi công đoạn cha — phải Resume công đoạn cha trước.");

        await ProjectScopeGuard.EnsureCanWriteTaskLeafAsync(task, _tasks, _currentUser, _authorizer, ct);

        task.Status = task.StatusBeforePause ?? TaskItemStatus.Todo;
        task.StatusBeforePause = null;
        await _tasks.UpdateAsync(task, ct);

        if (task.IsBatchParent)
            await _tasks.CascadeResumeChildrenAsync(task.Id, ct);

        return true;
    }
}
