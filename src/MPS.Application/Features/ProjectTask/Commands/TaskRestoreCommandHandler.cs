using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project;
using MPS.Application.Services;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProjectTask.Commands;

// Restore: CHỈ khôi phục được bản ghi bị Cancel TRỰC TIẾP (PausedByCascadeFromId == null).
// Nếu bị Cancel lan truyền từ Công đoạn cha, Công đoạn cha phải Restore trước (plan mục 6.5).
public class TaskRestoreCommandHandler : IRequestHandler<TaskRestoreCommand, bool>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskRestoreCommandHandler(
        IProjectTaskRepository tasks, IDepartmentManagerAuthorizer authorizer, ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(TaskRestoreCommand request, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy task #{request.Id}.");

        if (task.Status != TaskItemStatus.Cancelled)
            throw new DomainValidationException("Task không ở trạng thái Cancelled.");
        if (task.PausedByCascadeFromId is not null)
            throw new DomainValidationException("Task đang bị hủy bởi công đoạn cha — phải Restore công đoạn cha trước.");

        await ProjectScopeGuard.EnsureCanWriteTaskLeafAsync(task, _tasks, _currentUser, _authorizer, ct);

        task.Status = task.StatusBeforePause ?? TaskItemStatus.Todo;
        task.StatusBeforePause = null;
        task.DeletedAt = null;
        await _tasks.UpdateAsync(task, ct);

        if (task.IsBatchParent)
            await _tasks.CascadeRestoreChildrenAsync(task.Id, ct);

        return true;
    }
}
