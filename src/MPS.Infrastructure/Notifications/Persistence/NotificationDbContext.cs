using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Notifications;

namespace MPS.Infrastructure.Notifications.Persistence;

/// <summary>
/// DbContext RIÊNG cho module thông báo (bounded context độc lập).
/// Cùng database/connection string với AppDbContext nhưng bảng + lịch sử migration riêng
/// (xem MigrationsHistoryTable ở NotificationModuleRegistration / NotificationDbContextFactory).
///
/// QUAN TRỌNG: cấu hình entity đặt INLINE tại đây, KHÔNG dùng IEntityTypeConfiguration riêng —
/// để AppDbContext.ApplyConfigurationsFromAssembly KHÔNG vô tình nạp entity Notification vào model của nó.
/// </summary>
public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Notification>(builder =>
        {
            builder.ToTable("Notifications");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.RecipientUserId).IsRequired();
            builder.Property(e => e.Type).IsRequired();
            builder.Property(e => e.Category).IsRequired();
            builder.Property(e => e.Severity).IsRequired();

            builder.Property(e => e.Title).HasMaxLength(300).IsRequired();
            builder.Property(e => e.Message).HasMaxLength(2000).IsRequired();
            builder.Property(e => e.LinkUrl).HasMaxLength(500);
            builder.Property(e => e.MetadataJson).HasColumnType("nvarchar(max)");

            builder.Property(e => e.IsRead).IsRequired().HasDefaultValue(false);
            builder.Property(e => e.ReadAt);
            builder.Property(e => e.CreatedAt).IsRequired();
            builder.Property(e => e.UpdatedAt);

            // Đếm unread nhanh + list paged theo người nhận.
            builder.HasIndex(e => new { e.RecipientUserId, e.IsRead });
            builder.HasIndex(e => new { e.RecipientUserId, e.CreatedAt });
        });
    }
}
