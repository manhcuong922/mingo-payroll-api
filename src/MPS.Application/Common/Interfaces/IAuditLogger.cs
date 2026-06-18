using MPS.Domain.Enums;

namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Cross-cutting writer cho audit edge cases — chủ yếu là AccessDenied và escape hatch
/// thủ công. Đường ghi CRUD bình thường đi qua <c>AuditSaveChangesInterceptor</c>,
/// không đi qua interface này.
/// </summary>
public interface IAuditLogger
{
    Task LogAccessDeniedAsync(
        string requestPath,
        string? attemptedAction,
        CancellationToken cancellationToken = default);

    Task LogManualAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Snapshot tối thiểu để ghi 1 audit row bằng tay (escape hatch).
/// </summary>
public sealed record AuditLogEntry(
    string Module,
    AuditAction Action,
    AuditSeverity Severity,
    string RecordId,
    string DataDiff);
