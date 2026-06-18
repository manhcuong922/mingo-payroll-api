using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class AuditLogEntityConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Timestamp)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(e => e.UserId);

        builder.Property(e => e.UsernameSnapshot).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Module).HasMaxLength(80).IsRequired();
        builder.Property(e => e.Action).HasConversion<byte>().IsRequired();
        builder.Property(e => e.Severity).HasConversion<byte>().IsRequired();
        builder.Property(e => e.RecordId).HasMaxLength(64).IsRequired();
        builder.Property(e => e.DataDiff).IsRequired();
        builder.Property(e => e.IpAddress).HasMaxLength(45);
        builder.Property(e => e.RequestPath).HasMaxLength(256);
        builder.Property(e => e.CorrelationId).HasMaxLength(64);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.Timestamp)
            .HasDatabaseName("IX_AuditLogs_Timestamp")
            .IsDescending();

        builder.HasIndex(e => new { e.UserId, e.Timestamp })
            .HasDatabaseName("IX_AuditLogs_User_Timestamp")
            .IsDescending(false, true);

        builder.HasIndex(e => new { e.Module, e.RecordId, e.Timestamp })
            .HasDatabaseName("IX_AuditLogs_Record")
            .IsDescending(false, false, true);

        builder.HasIndex(e => new { e.Module, e.Timestamp })
            .HasDatabaseName("IX_AuditLogs_Module_Timestamp")
            .IsDescending(false, true);

        builder.HasIndex(e => new { e.Severity, e.Timestamp })
            .HasDatabaseName("IX_AuditLogs_Severity_Timestamp");
    }
}
