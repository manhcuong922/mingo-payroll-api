using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class CompanyPolicyFileEntityConfiguration : IEntityTypeConfiguration<CompanyPolicyFile>
{
    public void Configure(EntityTypeBuilder<CompanyPolicyFile> builder)
    {
        builder.ToTable("CompanyPolicyFiles");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.CompanyPolicyId).IsRequired();
        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(255).IsRequired();
        builder.Property(e => e.MinioBucket).HasMaxLength(100).IsRequired();
        builder.Property(e => e.MinioObjectKey).HasMaxLength(500).IsRequired();
        builder.Property(e => e.MimeType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.FileSizeBytes).IsRequired();
        builder.Property(e => e.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.IsCurrent).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.UploadedAt).IsRequired();

        builder.HasIndex(e => new { e.CompanyPolicyId, e.Version }).IsUnique();
        // Filtered unique: 1 file IsCurrent per policy.
        builder.HasIndex(e => e.CompanyPolicyId)
            .HasFilter("[IsCurrent] = 1")
            .IsUnique();

        builder.HasOne(e => e.UploadedByUser)
            .WithMany()
            .HasForeignKey(e => e.UploadedByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
