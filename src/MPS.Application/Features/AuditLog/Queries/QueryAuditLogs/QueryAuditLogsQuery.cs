using MediatR;
using MPS.Application.Common.Models;
using MPS.Domain.Enums;

namespace MPS.Application.Features.AuditLog.Queries.QueryAuditLogs;

public sealed record QueryAuditLogsQuery(
    int? UserId,
    string? Module,
    AuditAction? Action,
    AuditSeverity? Severity,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page,
    int Size) : IRequest<PagedResult<AuditLogDto>>;
