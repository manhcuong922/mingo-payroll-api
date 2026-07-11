namespace MPS.Infrastructure.Notifications.Persistence;

/// <summary>Hằng số dùng chung cho cấu hình NotificationDbContext (runtime + design-time).</summary>
public static class NotificationDbContextConstants
{
    /// <summary>
    /// Bảng lịch sử migration RIÊNG — tách khỏi __EFMigrationsHistory của AppDbContext
    /// để 2 DbContext cùng 1 database không tranh chấp lịch sử migration.
    /// </summary>
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_Notifications";
}
