using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project;
using MPS.Application.Services;
using MPS.Domain.Common;
using MPS.Domain.Constants;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using CommentEntity = MPS.Domain.Entities.Comment;

namespace MPS.Application.Features.Comment.Commands;

// Quyền viết Comment (plan mục 5): đi theo đúng quyền sửa entity gốc (Manager/PM), TRỪ Employee —
// ngoại lệ duy nhất, chỉ viết được trên Task của chính mình (Comment.Create.Own).
public class CommentCreateCommandHandler : IRequestHandler<CommentCreateCommand, int>
{
    private readonly ICommentRepository _comments;
    private readonly IProjectRepository _projects;
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public CommentCreateCommandHandler(
        ICommentRepository comments,
        IProjectRepository projects,
        IProjectTaskRepository tasks,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _comments = comments;
        _projects = projects;
        _tasks = tasks;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CommentCreateCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        if (string.IsNullOrWhiteSpace(request.Content))
            throw new DomainValidationException("Nội dung comment không được rỗng.");

        await EnsureCanWriteAsync(request, userId, ct);

        var comment = new CommentEntity
        {
            OwnerType = request.OwnerType,
            OwnerId = request.OwnerId,
            Content = request.Content.Trim(),
            AuthorUserId = userId,
            CreatedAt = VietnamClock.Now,
        };
        return await _comments.AddAsync(comment, ct);
    }

    private async Task EnsureCanWriteAsync(CommentCreateCommand request, int userId, CancellationToken ct)
    {
        if (request.OwnerType == CommentOwnerType.Project)
        {
            var project = await _projects.GetByIdAsync(request.OwnerId, ct)
                ?? throw new NotFoundException($"Không tìm thấy đơn #{request.OwnerId}.");
            await ProjectScopeGuard.EnsureCanWriteAsync(project.DepartmentId, _currentUser, _authorizer, ct);
            return;
        }

        var task = await _tasks.GetByIdAsync(request.OwnerId, ct)
            ?? throw new NotFoundException($"Không tìm thấy task #{request.OwnerId}.");

        if (_currentUser.HasPermission(Permissions.ProjectReadAll)) return; // GĐ/Phó GĐ

        // Planning Manager của xưởng task (refactor: bỏ per-stage PM).
        if (task.DepartmentId is int deptId && await _authorizer.IsManagerOfAsync(userId, deptId, ct))
            return;

        // Tổ trưởng sản xuất đang phụ trách đơn của task.
        if (task.ProjectId is int projectId)
        {
            var project = await _projects.GetByIdAsync(projectId, ct);
            if (project?.ResponsibleUserId == userId) return;
        }

        // Employee là assignee của chính task này (ngoại lệ Comment.Create.Own).
        if (_currentUser.HasPermission(Permissions.CommentCreateOwn))
        {
            var withGraph = await _tasks.GetByIdWithGraphAsync(task.Id, ct);
            if (withGraph?.Assignees.Any(a => a.UserId == userId) == true) return;
        }

        throw new ForbiddenAccessException("Bạn không có quyền viết comment trên task này.");
    }
}
