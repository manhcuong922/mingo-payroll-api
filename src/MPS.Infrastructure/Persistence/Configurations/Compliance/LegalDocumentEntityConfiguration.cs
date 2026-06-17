using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Compliance;

namespace MPS.Infrastructure.Persistence.Configurations.Compliance;

public sealed class LegalDocumentEntityConfiguration : IEntityTypeConfiguration<LegalDocument>
{
    public void Configure(EntityTypeBuilder<LegalDocument> builder)
    {
        builder.ToTable("LegalDocuments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Code).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique();

        builder.Property(e => e.DocumentType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.DocumentNumber).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.IssuedBy).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Url).HasMaxLength(2000);
        builder.Property(e => e.Note).HasMaxLength(2000);
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired();

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UpdatedByUser)
            .WithMany()
            .HasForeignKey(e => e.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
