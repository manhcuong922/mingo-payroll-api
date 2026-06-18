namespace MPS.Application.Options;

/// <summary>
/// Cấu hình retention cho audit log. Validation chi tiết ở
/// <c>InfrastructureServiceRegistration.AddOptions().Validate(...)</c>.
/// </summary>
public sealed class AuditLogOptions
{
    public const string SectionName = "AuditLog";

    /// <summary>
    /// Số ngày giữ row trong bảng active <c>AuditLogs</c>. Range 90–3650, default 365.
    /// Áp dụng cho mọi module (global).
    /// </summary>
    public int ActiveRetentionDays { get; init; } = 365;

    /// <summary>
    /// Số ngày giữ row Severity=Low trong <c>AuditLogsArchive</c> nếu module không có
    /// override. Phải ≥ <see cref="ActiveRetentionDays"/>. Default 1825 (5 năm).
    /// </summary>
    public int LowValueArchiveRetentionDays { get; init; } = 1825;

    /// <summary>
    /// Override per-module cho Low (tính từ Timestamp gốc, không cộng dồn active).
    /// Module không có trong dictionary → dùng <see cref="LowValueArchiveRetentionDays"/>.
    /// Chỉ áp dụng cho archive.
    /// </summary>
    public Dictionary<string, int> LowValueArchiveRetentionByModule { get; init; } = new();

    /// <summary>
    /// Số ngày giữ row Severity=High trong <c>AuditLogsArchive</c>.
    /// <c>null</c> = giữ vĩnh viễn (default).
    /// </summary>
    public int? HighValueArchiveRetentionDays { get; init; }
}
