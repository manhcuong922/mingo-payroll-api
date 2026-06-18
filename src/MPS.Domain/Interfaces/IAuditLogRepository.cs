using MPS.Domain.Entities;
using MPS.Domain.Enums;

namespace MPS.Domain.Interfaces;

public interface IAuditLogRepository
{
    Task<long> AddAsync(AuditLog log, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AuditLog> Items, int Total)> QueryPagedAsync(
        int? userId,
        string? module,
        AuditAction? action,
        AuditSeverity? severity,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int size,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLog>> GetRecordHistoryAsync(
        string module,
        string recordId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retention job — bước 1: move rows có Timestamp &lt; cutoff từ AuditLogs sang
    /// AuditLogsArchive (cùng transaction). Trả về số rows đã move.
    /// </summary>
    Task<int> MoveOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retention job — bước 2a: xóa rows trong AuditLogsArchive khớp <paramref name="severity"/>,
    /// thuộc đúng <paramref name="module"/>, có Timestamp &lt; cutoff. Per-module override.
    /// </summary>
    Task<int> DeleteFromArchiveForModuleAsync(
        AuditSeverity severity,
        string module,
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retention job — bước 2b: xóa rows trong AuditLogsArchive khớp <paramref name="severity"/>,
    /// KHÔNG thuộc <paramref name="excludeModules"/>, có Timestamp &lt; cutoff. Sweep default.
    /// </summary>
    Task<int> DeleteFromArchiveExceptModulesAsync(
        AuditSeverity severity,
        IReadOnlyCollection<string> excludeModules,
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retention job — bước 3: xóa rows trong AuditLogsArchive khớp <paramref name="severity"/>,
    /// có Timestamp &lt; cutoff. Dùng cho High với cấu hình global.
    /// </summary>
    Task<int> DeleteFromArchiveAsync(
        AuditSeverity severity,
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default);
}
