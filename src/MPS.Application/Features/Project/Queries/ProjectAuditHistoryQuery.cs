using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.AuditLog.Queries;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Project.Queries;

public record ProjectAuditHistoryQuery(int ProjectId) : IRequest<IReadOnlyList<AuditLogDto>>;

public class ProjectAuditHistoryQueryHandler
    : IRequestHandler<ProjectAuditHistoryQuery, IReadOnlyList<AuditLogDto>>
{
    private readonly IProjectRepository _projects;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public ProjectAuditHistoryQueryHandler(
        IProjectRepository projects,
        IAuditLogRepository auditLogs,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _projects = projects;
        _auditLogs = auditLogs;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AuditLogDto>> Handle(ProjectAuditHistoryQuery request, CancellationToken ct)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException($"Không tìm thấy đơn #{request.ProjectId}.");

        if (!_currentUser.HasPermission(Permissions.TaskAuditReadAll))
        {
            var userId = _currentUser.UserId;
            if (!_currentUser.HasPermission(Permissions.TaskAuditReadDepartment)
                || userId is null || project.DepartmentId is null
                || !await _authorizer.IsManagerOfAsync(userId.Value, project.DepartmentId.Value, ct))
            {
                throw new ForbiddenAccessException("Bạn không có quyền xem lịch sử của đơn này.");
            }
        }

        var logs = await _auditLogs.GetRecordHistoryAsync(nameof(MPS.Domain.Entities.Project), request.ProjectId.ToString(), ct);
        return logs.Select(AuditLogMapper.ToDto).ToList();
    }
}
