namespace MPS.Application.Features.AuditLog.Queries;

internal static class AuditLogMapper
{
    public static AuditLogDto ToDto(MPS.Domain.Entities.AuditLog log) => new(
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
