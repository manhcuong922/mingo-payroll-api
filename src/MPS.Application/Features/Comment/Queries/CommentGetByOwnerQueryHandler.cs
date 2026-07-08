using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Application.Features.Project;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Comment.Queries;

// Quyền đọc: GĐ/Phó GĐ đọc mọi tầng mọi xưởng; còn lại đi theo đúng quyền xem entity gốc —
// Project dùng scope xưởng (EnsureCanWriteAsync), Task dùng scope đọc task (EnsureCanReadTaskAsync,
// giống hệt TaskGetByIdQueryHandler) để mọi người xem được task đều xem được toàn bộ bình luận,
// không chỉ PM phụ trách/assignee của chính task.
public class CommentGetByOwnerQueryHandler : IRequestHandler<CommentGetByOwnerQuery, IReadOnlyList<CommentDto>>
{
    private readonly ICommentRepository _comments;
    private readonly IProjectRepository _projects;
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public CommentGetByOwnerQueryHandler(
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

    public async Task<IReadOnlyList<CommentDto>> Handle(CommentGetByOwnerQuery request, CancellationToken ct)
    {
        await EnsureCanReadAsync(request, ct);

        var items = await _comments.GetByOwnerAsync(request.OwnerType, request.OwnerId, ct);
        return items.Select(c => new CommentDto(
            c.Id, c.OwnerType.ToString(), c.OwnerId, c.Content, c.AuthorUserId, c.AuthorUser?.Username, c.CreatedAt))
            .ToList();
    }

    private async Task EnsureCanReadAsync(CommentGetByOwnerQuery request, CancellationToken ct)
    {
        if (_currentUser.HasPermission(Permissions.CommentReadAll)
            || _currentUser.HasPermission(Permissions.ProjectReadAll)) return; // GĐ/Phó GĐ

        if (request.OwnerType == CommentOwnerType.Project)
        {
            var project = await _projects.GetByIdAsync(request.OwnerId, ct)
                ?? throw new NotFoundException($"Không tìm thấy đơn #{request.OwnerId}.");
            await ProjectScopeGuard.EnsureCanWriteAsync(project.DepartmentId, _currentUser, _authorizer, ct);
            return;
        }

        var task = await _tasks.GetByIdWithGraphAsync(request.OwnerId, ct)
            ?? throw new NotFoundException($"Không tìm thấy task #{request.OwnerId}.");

        // Bình luận task hiển thị cho MỌI người có quyền xem task đó (không chỉ PM phụ trách/assignee) —
        // dùng chung scope đọc task với TaskGetByIdQueryHandler để nhất quán, tránh 1 nhóm bị chặn xem
        // toàn bộ thảo luận trong khi các thành viên khác vẫn xem/viết bình thường.
        await ProjectScopeGuard.EnsureCanReadTaskAsync(task, _tasks, _currentUser, _authorizer, ct);
    }
}
