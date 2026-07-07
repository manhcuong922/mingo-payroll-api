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

// Quyền đọc (plan mục 5): GĐ/Phó GĐ đọc mọi tầng mọi xưởng; còn lại đi theo quyền xem entity gốc
// (Manager/PM của đúng xưởng, hoặc Employee là assignee của chính Task đó).
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

        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        if (task.AssignedPmUserId == userId) return;
        if (task.Assignees.Any(a => a.UserId == userId)) return;

        var parentPm = task.ParentTaskId is int parentId
            ? (await _tasks.GetByIdAsync(parentId, ct))?.AssignedPmUserId
            : null;
        if (parentPm == userId) return;

        // Plan mục 5: Task chỉ đọc được bởi PM phụ trách công đoạn cha / assignee của chính task / GĐ —
        // KHÔNG mở rộng cho "bất kỳ ai cùng xưởng" (khác Project/Công đoạn, vốn dùng EnsureCanWriteAsync).
        throw new ForbiddenAccessException("Bạn không có quyền xem bình luận của task này.");
    }
}
