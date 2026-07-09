using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Services;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Project.Commands;

public class ProjectUpdateCommandHandler : IRequestHandler<ProjectUpdateCommand, bool>
{
    private readonly IProjectRepository _repository;
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public ProjectUpdateCommandHandler(
        IProjectRepository repository,
        IProjectTaskRepository tasks,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _tasks = tasks;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(ProjectUpdateCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy đơn #{request.Id}.");

        // Lập kế hoạch: Planning Manager chỉ sửa đơn xưởng mình; GĐ mọi xưởng.
        await ProjectScopeGuard.EnsureCanWriteAsync(entity.DepartmentId, _currentUser, _authorizer, cancellationToken);

        var oldStart = entity.StartDate;
        var oldDeadline = entity.Deadline;

        entity.Title = request.Title.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        // Partial-update semantics: null = giữ nguyên (đối xứng với StartDate). Tránh trường hợp client gửi
        // update Title-only không kèm Deadline → JSON default null → wipe deadline + cascade toàn bộ task về null.
        // Muốn xoá deadline phải qua endpoint riêng (hiện chưa mở).
        if (request.StartDate is { } start) entity.StartDate = start;
        if (request.Deadline is { } dl) entity.Deadline = dl;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<ProjectStatus>(request.Status, ignoreCase: true, out var status))
                throw new DomainValidationException($"Trạng thái đơn không hợp lệ: {request.Status}.");
            entity.Status = status;
        }

        if (entity.Deadline is { } deadline && deadline < entity.StartDate.ToDateTime(TimeOnly.MinValue))
            throw new DomainValidationException("Deadline đơn không được trước StartDate.");

        var ok = await _repository.UpdateAsync(entity, cancellationToken);

        // Cascade co giãn deadline mọi task theo ratio khi timeline đơn đổi (refactor TM).
        if (ok && (entity.StartDate != oldStart || entity.Deadline != oldDeadline))
            await _tasks.RecomputeDeadlinesFromRatioAsync(entity.Id, entity.StartDate, entity.Deadline, cancellationToken);

        return ok;
    }
}
