using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class AuditLogArchiveEntityConfiguration : IEntityTypeConfiguration<AuditLogArchive>
{
    public void Configure(EntityTypeBuilder<AuditLogArchive> builder)
    {
        builder.ToTable("AuditLogsArchive");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Timestamp).HasColumnType("datetime2").IsRequired();
        builder.Property(e => e.ArchivedAt).HasColumnType("datetime2").IsRequired();

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

        builder.HasIndex(e => e.Timestamp)
            .HasDatabaseName("IX_AuditLogsArchive_Timestamp")
            .IsDescending();

        builder.HasIndex(e => new { e.UserId, e.Timestamp })
            .HasDatabaseName("IX_AuditLogsArchive_User_Timestamp")
            .IsDescending(false, true);

        builder.HasIndex(e => new { e.Module, e.RecordId, e.Timestamp })
            .HasDatabaseName("IX_AuditLogsArchive_Record")
            .IsDescending(false, false, true);

        builder.HasIndex(e => new { e.Module, e.Timestamp })
            .HasDatabaseName("IX_AuditLogsArchive_Module_Timestamp")
            .IsDescending(false, true);

        builder.HasIndex(e => new { e.Severity, e.Timestamp })
            .HasDatabaseName("IX_AuditLogsArchive_Severity_Timestamp");
    }
}
