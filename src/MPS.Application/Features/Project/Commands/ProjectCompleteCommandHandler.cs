using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Services;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Project.Commands;

public class ProjectCompleteCommandHandler : IRequestHandler<ProjectCompleteCommand, bool>
{
    private readonly IProjectRepository _projects;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public ProjectCompleteCommandHandler(
        IProjectRepository projects,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _projects = projects;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(ProjectCompleteCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        var project = await _projects.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy đơn #{request.Id}.");

        if (project.DepartmentId is null || !await _authorizer.IsManagerOfAsync(userId, project.DepartmentId.Value, ct))
            throw new ForbiddenAccessException("Bạn không phải quản lý của xưởng thực hiện đơn này.");

        project.Status = ProjectStatus.Completed;
        project.CompletedByUserId = userId;
        project.CompletedAt = DateTime.UtcNow;
        project.CompletionNote = string.IsNullOrWhiteSpace(request.CompletionNote)
            ? null : request.CompletionNote.Trim();

        return await _projects.UpdateAsync(project, ct);
    }
}
