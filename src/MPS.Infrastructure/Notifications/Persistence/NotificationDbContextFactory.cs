using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MPS.Infrastructure.Notifications.Persistence;

/// <summary>
/// Design-time factory cho EF Core tools (migrations) của NotificationDbContext.
/// Lấy connection string từ appsettings.json của MPS.API (cùng nguồn runtime) và dùng
/// bảng lịch sử migration RIÊNG để không đụng __EFMigrationsHistory của AppDbContext.
/// </summary>
public class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var apiProjectPath = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "MPS.API"));

        if (!Directory.Exists(apiProjectPath))
            apiProjectPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "MPS.API");

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured for design-time factory.");

        var optionsBuilder = new DbContextOptionsBuilder<NotificationDbContext>();
        optionsBuilder.UseSqlServer(
            connectionString,
            sql => sql.MigrationsHistoryTable(NotificationDbContextConstants.MigrationsHistoryTable));

        return new NotificationDbContext(optionsBuilder.Options);
    }
}
