using MediatR;

namespace MPS.Application.Features.AuditLog.Queries.GetRecordHistory;

public sealed record GetRecordHistoryQuery(
    string Module,
    string RecordId) : IRequest<IReadOnlyList<AuditLogDto>>;
