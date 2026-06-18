using MPS.Domain.Enums;

namespace MPS.Application.Features.AuditLog.Queries;

public sealed record AuditLogDto(
    long Id,
    DateTime Timestamp,
    int? UserId,
    string UsernameSnapshot,
    string Module,
    AuditAction Action,
    AuditSeverity Severity,
    string RecordId,
    string DataDiff,
    string? IpAddress,
    string? RequestPath,
    string? CorrelationId);
