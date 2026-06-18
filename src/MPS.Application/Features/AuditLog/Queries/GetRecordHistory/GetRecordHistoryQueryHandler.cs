using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.AuditLog.Queries.GetRecordHistory;

public class GetRecordHistoryQueryHandler
    : IRequestHandler<GetRecordHistoryQuery, IReadOnlyList<AuditLogDto>>
{
    private readonly IAuditLogRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetRecordHistoryQueryHandler(IAuditLogRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AuditLogDto>> Handle(
        GetRecordHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(Permissions.AuditLogReadAll))
            throw new ForbiddenAccessException("Caller lacks AuditLog.Read.All permission.");

        var items = await _repository.GetRecordHistoryAsync(request.Module, request.RecordId, cancellationToken);

        return items.Select(log => new AuditLogDto(
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
                log.CorrelationId))
            .ToList();
    }
}
