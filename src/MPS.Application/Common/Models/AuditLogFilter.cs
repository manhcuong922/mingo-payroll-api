using MPS.Domain.Enums;

namespace MPS.Application.Common.Models;

public sealed record AuditLogFilter(
    int? UserId,
    string? Module,
    AuditAction? Action,
    AuditSeverity? Severity,
    DateTime? FromUtc,
    DateTime? ToUtc);
