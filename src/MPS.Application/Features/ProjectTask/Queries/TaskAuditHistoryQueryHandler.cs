using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.AuditLog.Queries;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProjectTask.Queries;

public class TaskAuditHistoryQueryHandler
    : IRequestHandler<TaskAuditHistoryQuery, IReadOnlyList<AuditLogDto>>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskAuditHistoryQueryHandler(
        IProjectTaskRepository tasks,
        IAuditLogRepository auditLogs,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _auditLogs = auditLogs;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AuditLogDto>> Handle(TaskAuditHistoryQuery request, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(request.TaskId, ct)
            ?? throw new NotFoundException($"Không tìm thấy task #{request.TaskId}.");

        // ⭐ #5: ReadAll xem mọi xưởng; ReadDepartment chỉ xưởng mình (chống truy cập chéo → 403).
        if (!_currentUser.HasPermission(Permissions.TaskAuditReadAll))
        {
            var userId = _currentUser.UserId;
            if (!_currentUser.HasPermission(Permissions.TaskAuditReadDepartment)
                || userId is null || task.DepartmentId is null
                || !await _authorizer.IsManagerOfAsync(userId.Value, task.DepartmentId.Value, ct))
            {
                throw new ForbiddenAccessException("Bạn không có quyền xem lịch sử của task này.");
            }
        }

        var logs = await _auditLogs.GetRecordHistoryAsync(nameof(MPS.Domain.Entities.ProjectTask), request.TaskId.ToString(), ct);
        return logs.Select(AuditLogMapper.ToDto).ToList();
    }
}
