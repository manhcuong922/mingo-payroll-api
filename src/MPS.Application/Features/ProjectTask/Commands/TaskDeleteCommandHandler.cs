using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Services;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProjectTask.Commands;

public class TaskDeleteCommandHandler : IRequestHandler<TaskDeleteCommand, bool>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskDeleteCommandHandler(
        IProjectTaskRepository tasks,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(TaskDeleteCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        var task = await _tasks.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy task #{request.Id}.");

        if (task.DepartmentId is null || !await _authorizer.IsManagerOfAsync(userId, task.DepartmentId.Value, ct))
            throw new ForbiddenAccessException("Bạn không phải quản lý của xưởng chứa task này.");

        if (await _tasks.HasChildrenAsync(task.Id, ct))
            throw new DomainValidationException("Không thể xóa công đoạn còn task con — xóa các con trước.");

        return await _tasks.DeleteAsync(task.Id, ct);
    }
}
