using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class ProductionAttachmentEntityConfiguration : IEntityTypeConfiguration<ProductionAttachment>
{
    public void Configure(EntityTypeBuilder<ProductionAttachment> builder)
    {
        builder.ToTable("ProductionAttachments");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.OwnerType).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ObjectKey).HasMaxLength(512).IsRequired();
        builder.Property(e => e.Bucket).HasMaxLength(100).IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(260).IsRequired();
        builder.Property(e => e.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Sha256Hex).HasMaxLength(64);
        builder.Property(e => e.Note).HasMaxLength(500);

        builder.HasOne(a => a.UploadedBy)
            .WithMany()
            .HasForeignKey(a => a.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Polymorphic (OwnerType + OwnerId) — KHÔNG FK cứng tới Project/Task.
        builder.HasIndex(a => new { a.OwnerType, a.OwnerId });
    }
}
