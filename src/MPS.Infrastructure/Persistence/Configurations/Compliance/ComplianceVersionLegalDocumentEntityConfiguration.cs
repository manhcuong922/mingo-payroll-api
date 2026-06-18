using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Compliance;

namespace MPS.Infrastructure.Persistence.Configurations.Compliance;

public sealed class ComplianceVersionLegalDocumentEntityConfiguration : IEntityTypeConfiguration<ComplianceVersionLegalDocument>
{
    public void Configure(EntityTypeBuilder<ComplianceVersionLegalDocument> builder)
    {
        builder.ToTable("ComplianceVersionLegalDocuments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.ComplianceEntityType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.ComplianceVersionId).IsRequired();
        builder.Property(e => e.LinkedAt).IsRequired();
        builder.Property(e => e.LinkedByUserId).IsRequired();

        // Unique: không link trùng văn bản cho cùng 1 version
        builder.HasIndex(e => new { e.ComplianceEntityType, e.ComplianceVersionId, e.LegalDocumentId })
            .IsUnique();

        builder.HasOne(e => e.LegalDocument)
            .WithMany(d => d.ComplianceVersions)
            .HasForeignKey(e => e.LegalDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.LinkedByUser)
            .WithMany()
            .HasForeignKey(e => e.LinkedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
