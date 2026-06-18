using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.AuditLog.Queries.QueryAuditLogs;

public class QueryAuditLogsQueryHandler : IRequestHandler<QueryAuditLogsQuery, PagedResult<AuditLogDto>>
{
    private const int MaxPageSize = 50;

    private readonly IAuditLogRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public QueryAuditLogsQueryHandler(IAuditLogRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AuditLogDto>> Handle(QueryAuditLogsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(Permissions.AuditLogReadAll))
            throw new ForbiddenAccessException("Caller lacks AuditLog.Read.All permission.");

        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, MaxPageSize);

        var (items, total) = await _repository.QueryPagedAsync(
            request.UserId,
            request.Module,
            request.Action,
            request.Severity,
            request.FromUtc,
            request.ToUtc,
            page,
            size,
            cancellationToken);

        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<AuditLogDto>(dtos, total, page, size);
    }

    private static AuditLogDto MapToDto(Domain.Entities.AuditLog log) => new(
        log.Id,
        log.Timestamp,
        log.UserId,
        log.UsernameSnapshot,
        log.Module,
        log.Action,
        log.Severity,
        log.RecordId,
        log.DataDiff,
        log.IpAddress,
        log.RequestPath,
        log.CorrelationId);
}
