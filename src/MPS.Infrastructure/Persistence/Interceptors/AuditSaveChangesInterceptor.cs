using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Attributes;
using MPS.Domain.Entities;
using MPS.Domain.Enums;

namespace MPS.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Capture mọi entity có <see cref="AuditableAttribute"/> trong SaveChanges và append
/// row tương ứng vào <c>AuditLogs</c> trong cùng transaction. Loop guard bỏ qua
/// chính AuditLog/AuditLogArchive. Mọi lỗi đều được throw lên để spec
/// "ghi log lỗi → block hành động CRUD" hoạt động đúng.
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = null,
    };

    private readonly ICurrentUserService _currentUser;
    private readonly IAuditContextProvider _auditContext;
    private readonly IAuditSeverityClassifier _classifier;

    public AuditSaveChangesInterceptor(
        ICurrentUserService currentUser,
        IAuditContextProvider auditContext,
        IAuditSeverityClassifier classifier)
    {
        _currentUser = currentUser;
        _auditContext = auditContext;
        _classifier = classifier;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        CaptureAuditEntries(context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        var context = eventData.Context;
        if (context is null)
            return base.SavingChanges(eventData, result);

        CaptureAuditEntries(context);

        return base.SavingChanges(eventData, result);
    }

    private void CaptureAuditEntries(DbContext context)
    {
        var entries = context.ChangeTracker
            .Entries()
            .Where(IsAuditable)
            .ToList();

        if (entries.Count == 0) return;

        var nowUtc = DateTime.UtcNow;
        var userId = _currentUser.UserId;
        var usernameSnapshot = _currentUser.Username ?? "system";
        var ip = _auditContext.IpAddress;
        var path = _auditContext.RequestPath;
        var corr = _auditContext.CorrelationId;

        foreach (var entry in entries)
        {
            var action = MapAction(entry.State);
            if (action is null) continue;

            var module = entry.Entity.GetType().Name;
            var recordId = ResolveRecordId(entry);
            var (diff, changedFields) = BuildDiff(entry, action.Value);

            // Update không có field nào thay đổi thật sự → skip (EF có thể mark Modified
            // sau set property cùng giá trị cũ).
            if (action == AuditAction.Update && changedFields.Count == 0)
                continue;

            var severity = _classifier.Classify(module, action.Value, changedFields);

            var log = new AuditLog
            {
                Timestamp = nowUtc,
                UserId = userId,
                UsernameSnapshot = Truncate(usernameSnapshot, 100)!,
                Module = Truncate(module, 80)!,
                Action = action.Value,
                Severity = severity,
                RecordId = Truncate(recordId, 64)!,
                DataDiff = diff,
                IpAddress = Truncate(ip, 45),
                RequestPath = Truncate(path, 256),
                CorrelationId = Truncate(corr, 64),
            };

            context.Add(log);
        }
    }

    private static bool IsAuditable(EntityEntry entry)
    {
        if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            return false;

        var type = entry.Entity.GetType();
        // Loop guard
        if (type == typeof(AuditLog) || type == typeof(AuditLogArchive))
            return false;

        return type.GetCustomAttributes(typeof(AuditableAttribute), inherit: true).Length > 0;
    }

    private static AuditAction? MapAction(EntityState state) => state switch
    {
        EntityState.Added => AuditAction.Create,
        EntityState.Modified => AuditAction.Update,
        EntityState.Deleted => AuditAction.Delete,
        _ => null,
    };

    private static string ResolveRecordId(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null) return string.Empty;

        var values = key.Properties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? string.Empty);
        return string.Join("|", values);
    }

    private static (string Diff, IReadOnlyCollection<string> ChangedFields) BuildDiff(
        EntityEntry entry,
        AuditAction action)
    {
        var props = entry.Properties
            .Where(p => !p.Metadata.IsPrimaryKey())
            .ToList();

        switch (action)
        {
            case AuditAction.Create:
            {
                var payload = props.ToDictionary(
                    p => p.Metadata.Name,
                    p => (object?)new { @new = p.CurrentValue });
                return (Serialize(payload), props.Select(p => p.Metadata.Name).ToList());
            }

            case AuditAction.Delete:
            {
                var payload = props.ToDictionary(
                    p => p.Metadata.Name,
                    p => (object?)new { old = p.OriginalValue });
                return (Serialize(payload), props.Select(p => p.Metadata.Name).ToList());
            }

            case AuditAction.Update:
            {
                var changed = props
                    .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
                    .ToList();
                var payload = changed.ToDictionary(
                    p => p.Metadata.Name,
                    p => (object?)new { old = p.OriginalValue, @new = p.CurrentValue });
                return (Serialize(payload), changed.Select(p => p.Metadata.Name).ToList());
            }

            default:
                return ("{}", Array.Empty<string>());
        }
    }

    private static string Serialize(Dictionary<string, object?> payload) =>
        JsonSerializer.Serialize(payload, JsonOptions);

    private static string? Truncate(string? value, int max)
    {
        if (value is null) return null;
        return value.Length <= max ? value : value[..max];
    }
}
