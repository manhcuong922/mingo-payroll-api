using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProjectTask.Commands;

public class TaskAssignPmCommandHandler : IRequestHandler<TaskAssignPmCommand, bool>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentRepository _departments;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskAssignPmCommandHandler(
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

    public async Task<bool> Handle(TaskAssignPmCommand request, CancellationToken ct)
    {
        var stage = await _tasks.GetByIdAsync(request.StageId, ct)
            ?? throw new NotFoundException($"Không tìm thấy công đoạn #{request.StageId}.");

        if (!stage.IsBatchParent)
            throw new DomainValidationException("Chỉ gán PM cho Công đoạn (IsBatchParent=true).");

        // Chỉ Manager của xưởng đó / GĐ được đổi PM — không phải chính PM đang phụ trách.
        await ProjectScopeGuard.EnsureCanWriteTaskAsync(stage.DepartmentId, _currentUser, _authorizer, ct);

        var isValidPm = await _departments.IsUserInDepartmentWithRoleAsync(
            request.PmUserId, stage.DepartmentId ?? 0, SystemRoles.ProjectManagerId, ct);
        if (!isValidPm)
            throw new DomainValidationException("PM được gán phải có role ProjectManager và cùng xưởng với công đoạn.");

        stage.AssignedPmUserId = request.PmUserId;
        return await _tasks.UpdateAsync(stage, ct);
    }
}
