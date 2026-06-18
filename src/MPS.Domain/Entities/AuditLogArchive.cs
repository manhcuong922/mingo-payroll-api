using MPS.Domain.Enums;

namespace MPS.Domain.Entities;

/// <summary>
/// Cold storage cho audit log. Rows được move từ <see cref="AuditLog"/> bởi
/// <c>AuditLogRetentionJob</c> sau khi vượt <c>ActiveRetentionDays</c>.
/// </summary>
public class AuditLogArchive
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }

    public int? UserId { get; set; }

    public string UsernameSnapshot { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public AuditSeverity Severity { get; set; }
    public string RecordId { get; set; } = string.Empty;
    public string DataDiff { get; set; } = string.Empty;

    public string? IpAddress { get; set; }
    public string? RequestPath { get; set; }
    public string? CorrelationId { get; set; }

    public DateTime ArchivedAt { get; set; }
}
