using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class ContractFileEntityConfiguration : IEntityTypeConfiguration<ContractFile>
{
    public void Configure(EntityTypeBuilder<ContractFile> builder)
    {
        builder.ToTable("ContractFiles");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.TargetType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.TargetId).IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(255).IsRequired();
        builder.Property(e => e.MinioBucket).HasMaxLength(100).IsRequired();
        builder.Property(e => e.MinioObjectKey).HasMaxLength(500).IsRequired();
        builder.Property(e => e.CdnUrl).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.MimeType).HasMaxLength(100).IsRequired().HasDefaultValue("application/pdf");
        builder.Property(e => e.FileSizeBytes).IsRequired();
        builder.Property(e => e.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(e => e.Version).IsRequired().HasDefaultValue(1);
        builder.Property(e => e.IsCurrent).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.SignedBySource).HasMaxLength(100);
        builder.Property(e => e.UploadedAt).IsRequired();

        builder.HasIndex(e => new { e.TargetType, e.TargetId, e.IsCurrent });

        builder.HasOne(e => e.UploadedByUser)
            .WithMany()
            .HasForeignKey(e => e.UploadedByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
