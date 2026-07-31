using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MPS.Application.Features.Notifications;
using MPS.Application.Features.Notifications.Abstractions;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Application.Features.Notifications.Templates;
using MPS.Domain.Interfaces.Notifications;
using MPS.Infrastructure.Notifications.Persistence;
using MPS.Infrastructure.Notifications.Realtime;
using MPS.Infrastructure.Notifications.Recipients;

namespace MPS.Infrastructure.Notifications;

/// <summary>
/// Điểm đăng ký DUY NHẤT của module Thông báo. Gọi 1 dòng ở Program.cs:
/// <c>builder.Services.AddNotificationModule(builder.Configuration);</c>
/// Module tự chứa — KHÔNG đụng AppDbContext / DI của module khác.
/// </summary>
public static class NotificationModuleRegistration
{
    public static IServiceCollection AddNotificationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured for NotificationModule.");

        // DbContext RIÊNG — cùng DB nhưng bảng lịch sử migration riêng.
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(NotificationDbContext).Assembly.FullName);
                sql.MigrationsHistoryTable(NotificationDbContextConstants.MigrationsHistoryTable);
            }));

        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationService, NotificationService>();

        // Phase 1: no-op. Phase 2 thay bằng SignalRRealtimePush ở đây (1 dòng).
        services.AddScoped<IRealtimePush, NullRealtimePush>();

        // ===== Khung phát thông báo (Phase 3) =====
        // Danh bạ người nhận (đọc AppDbContext) + resolver + notifier điều phối.
        services.AddScoped<INotificationRecipientDirectory, NotificationRecipientDirectory>();
        services.AddScoped<IRecipientResolver, RecipientResolver>();
        // Phase 1 kênh email: resolver tra User.Email / EmployeeInformation.Email theo prefersPersonal.
        services.AddScoped<IUserEmailResolver, UserEmailResolver>();
        services.AddScoped<INotifier, Notifier>();

        // Auto-scan MỌI template trong assembly Application → thêm 1 file template là tự chạy,
        // KHÔNG cần sửa đăng ký DI ở đây.
        RegisterTemplates(services);

        return services;
    }

    /// <summary>
    /// Quét & đăng ký mọi INotificationTemplate cụ thể trong assembly chứa các template
    /// (MPS.Application). Đăng ký dưới interface INotificationTemplate để Notifier nhận qua IEnumerable.
    /// </summary>
    private static void RegisterTemplates(IServiceCollection services)
    {
        var templateContract = typeof(INotificationTemplate);
        var templateTypes = templateContract.Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && templateContract.IsAssignableFrom(t));

        foreach (var type in templateTypes)
            services.AddScoped(templateContract, type);
    }
}
