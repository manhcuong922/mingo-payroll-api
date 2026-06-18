using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Compliance.AutoUpdate;

namespace MPS.Infrastructure.Persistence.Configurations.Compliance.AutoUpdate;

public sealed class SourceSnapshotEntityConfiguration : IEntityTypeConfiguration<SourceSnapshot>
{
    public void Configure(EntityTypeBuilder<SourceSnapshot> builder)
    {
        builder.ToTable("ComplianceSourceSnapshots");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.SourceKey).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Fingerprint).HasMaxLength(64).IsRequired();
        builder.Property(e => e.CheckedAt).IsRequired();
        builder.HasIndex(e => e.SourceKey).IsUnique();
    }
}

public sealed class CrawledDocumentLogEntityConfiguration : IEntityTypeConfiguration<CrawledDocumentLog>
{
    public void Configure(EntityTypeBuilder<CrawledDocumentLog> builder)
    {
        builder.ToTable("ComplianceCrawledDocumentLogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.SourceKey).HasMaxLength(50).IsRequired();
        builder.Property(e => e.DocumentUrl).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.UrlHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.DocumentNo).HasMaxLength(100);
        builder.Property(e => e.Title).HasMaxLength(1000);
        builder.Property(e => e.ExtractStatus).HasMaxLength(30).IsRequired();
        builder.Property(e => e.FailureReason).HasMaxLength(2000);
        builder.Property(e => e.FirstSeenAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.HasIndex(e => e.UrlHash).IsUnique();
        builder.HasIndex(e => e.SourceKey);
    }
}

public sealed class ComplianceUpdateProposalEntityConfiguration : IEntityTypeConfiguration<ComplianceUpdateProposal>
{
    public void Configure(EntityTypeBuilder<ComplianceUpdateProposal> builder)
    {
        builder.ToTable("ComplianceUpdateProposals");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.SourceKey).HasMaxLength(50).IsRequired();
        builder.Property(e => e.DocumentUrl).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.DocumentNo).HasMaxLength(100);
        builder.Property(e => e.TargetEntityType).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ExtractedJson).IsRequired();
        builder.Property(e => e.DiffJson).IsRequired();
        builder.Property(e => e.ValidationReport).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(30).IsRequired();
        builder.Property(e => e.ReviewNote).HasMaxLength(2000);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.HasIndex(e => new { e.Status, e.CreatedAt });
    }
}
