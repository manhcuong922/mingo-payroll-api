using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class EmailOutboxMessageEntityConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.ToTable("EmailOutboxMessages");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Type).HasConversion<byte>().IsRequired();
        builder.Property(e => e.ToEmail).HasMaxLength(256).IsRequired();
        builder.Property(e => e.PayloadJson).IsRequired();

        builder.Property(e => e.StorageBucket).HasMaxLength(128);
        builder.Property(e => e.StorageObjectKey).HasMaxLength(512);

        builder.Property(e => e.Status).HasConversion<byte>().IsRequired();
        builder.Property(e => e.Attempts).IsRequired();
        builder.Property(e => e.NextAttemptAt).HasColumnType("datetime2");
        builder.Property(e => e.LastError).HasMaxLength(2000);

        builder.Property(e => e.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();
        builder.Property(e => e.SentAt).HasColumnType("datetime2");

        // Câu poll của worker: WHERE Status=Pending AND NextAttemptAt<=now ORDER BY CreatedAt.
        builder.HasIndex(e => new { e.Status, e.NextAttemptAt })
            .HasDatabaseName("IX_EmailOutbox_Status_NextAttempt");
    }
}
