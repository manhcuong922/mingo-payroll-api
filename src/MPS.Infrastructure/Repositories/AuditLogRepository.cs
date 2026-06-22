using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<long> AddAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        await _context.AuditLogs.AddAsync(log, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return log.Id;
    }

    public async Task<(IReadOnlyList<AuditLog> Items, int Total)> QueryPagedAsync(
        int? userId,
        string? module,
        AuditAction? action,
        AuditSeverity? severity,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (userId.HasValue)
            query = query.Where(x => x.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(module))
            query = query.Where(x => x.Module == module);

        if (action.HasValue)
            query = query.Where(x => x.Action == action.Value);

        if (severity.HasValue)
            query = query.Where(x => x.Severity == severity.Value);

        if (fromUtc.HasValue)
            query = query.Where(x => x.Timestamp >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(x => x.Timestamp <= toUtc.Value);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<AuditLog>> GetRecordHistoryAsync(
        string module,
        string recordId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .AsNoTracking()
            .Where(x => x.Module == module && x.RecordId == recordId)
            .OrderBy(x => x.Timestamp)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> MoveOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        var toMove = await _context.AuditLogs
            .Where(x => x.Timestamp < cutoffUtc)
            .ToListAsync(cancellationToken);

        if (toMove.Count == 0)
        {
            await tx.CommitAsync(cancellationToken);
            return 0;
        }

        var nowUtc = DateTime.UtcNow;
        var archives = toMove.Select(x => new AuditLogArchive
        {
            Id = x.Id,
            Timestamp = x.Timestamp,
            UserId = x.UserId,
            UsernameSnapshot = x.UsernameSnapshot,
            Module = x.Module,
            Action = x.Action,
            Severity = x.Severity,
            RecordId = x.RecordId,
            DataDiff = x.DataDiff,
            IpAddress = x.IpAddress,
            RequestPath = x.RequestPath,
            CorrelationId = x.CorrelationId,
            ArchivedAt = nowUtc,
        }).ToList();

        await _context.AuditLogsArchive.AddRangeAsync(archives, cancellationToken);
        _context.AuditLogs.RemoveRange(toMove);

        var moved = await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return toMove.Count;
    }

    public async Task<int> DeleteFromArchiveForModuleAsync(
        AuditSeverity severity,
        string module,
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogsArchive
            .Where(x => x.Severity == severity && x.Module == module && x.Timestamp < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteFromArchiveExceptModulesAsync(
        AuditSeverity severity,
        IReadOnlyCollection<string> excludeModules,
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogsArchive
            .Where(x => x.Severity == severity && x.Timestamp < cutoffUtc);

        if (excludeModules.Count > 0)
            query = query.Where(x => !excludeModules.Contains(x.Module));

        return await query.ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteFromArchiveAsync(
        AuditSeverity severity,
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogsArchive
            .Where(x => x.Severity == severity && x.Timestamp < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
