using MPS.Domain.Enums;

namespace MPS.Domain.Entities;

/// <summary>
/// Append-only audit trail. Ghi tự động bởi AuditSaveChangesInterceptor cho mọi entity
/// có <see cref="MPS.Domain.Attributes.AuditableAttribute"/>. AccessDenied được ghi
/// trực tiếp bởi <c>IAuditLogger</c>.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public string UsernameSnapshot { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public AuditSeverity Severity { get; set; }
    public string RecordId { get; set; } = string.Empty;
    public string DataDiff { get; set; } = string.Empty;

    public string? IpAddress { get; set; }
    public string? RequestPath { get; set; }
    public string? CorrelationId { get; set; }
}
