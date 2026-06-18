using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Services;

/// <summary>
/// Ghi audit rows ngoài luồng SaveChanges thường (chủ yếu AccessDenied + escape hatch).
/// Ghi trực tiếp qua <see cref="AppDbContext"/>; loop guard ở interceptor đảm bảo
/// không lặp khi context SaveChanges chạy.
/// </summary>
public sealed class AuditLogger : IAuditLogger
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditContextProvider _auditContext;
    private readonly IAuditSeverityClassifier _classifier;

    public AuditLogger(
        AppDbContext context,
        ICurrentUserService currentUser,
        IAuditContextProvider auditContext,
        IAuditSeverityClassifier classifier)
    {
        _context = context;
        _currentUser = currentUser;
        _auditContext = auditContext;
        _classifier = classifier;
    }

    public async Task LogAccessDeniedAsync(
        string requestPath,
        string? attemptedAction,
        CancellationToken cancellationToken = default)
    {
        const string module = "AccessControl";
        var severity = _classifier.Classify(module, AuditAction.AccessDenied, Array.Empty<string>());

        var log = new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            UserId = _currentUser.UserId,
            UsernameSnapshot = Truncate(_currentUser.Username ?? "anonymous", 100)!,
            Module = module,
            Action = AuditAction.AccessDenied,
            Severity = severity,
            RecordId = Truncate(attemptedAction, 64) ?? string.Empty,
            DataDiff = "{}",
            IpAddress = Truncate(_auditContext.IpAddress, 45),
            RequestPath = Truncate(requestPath, 256),
            CorrelationId = Truncate(_auditContext.CorrelationId, 64),
        };

        await _context.AuditLogs.AddAsync(log, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task LogManualAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        var log = new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            UserId = _currentUser.UserId,
            UsernameSnapshot = Truncate(_currentUser.Username ?? "system", 100)!,
            Module = Truncate(entry.Module, 80)!,
            Action = entry.Action,
            Severity = entry.Severity,
            RecordId = Truncate(entry.RecordId, 64) ?? string.Empty,
            DataDiff = entry.DataDiff,
            IpAddress = Truncate(_auditContext.IpAddress, 45),
            RequestPath = Truncate(_auditContext.RequestPath, 256),
            CorrelationId = Truncate(_auditContext.CorrelationId, 64),
        };

        await _context.AuditLogs.AddAsync(log, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string? Truncate(string? value, int max)
    {
        if (value is null) return null;
        return value.Length <= max ? value : value[..max];
    }
}
