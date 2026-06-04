using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class ContractTerminationEntityConfiguration : IEntityTypeConfiguration<ContractTermination>
{
    public void Configure(EntityTypeBuilder<ContractTermination> builder)
    {
        builder.ToTable("ContractTerminations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.TerminationDate).IsRequired();
        builder.Property(e => e.TerminationParty).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.LegalBasis).HasMaxLength(200).IsRequired();
        builder.Property(e => e.NoticeSentAt);
        builder.Property(e => e.NoticeDaysAhead);
        builder.Property(e => e.NoNoticeRequired).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.Notes);
        builder.Property(e => e.CreatedAt).IsRequired();

        // 1-1 với Contract
        builder.HasIndex(e => e.ContractId).IsUnique();

        builder.HasOne(e => e.Contract)
            .WithOne(c => c.ContractTermination)
            .HasForeignKey<ContractTermination>(e => e.ContractId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ReasonRef)
            .WithMany(r => r.ContractTerminations)
            .HasForeignKey(e => e.ReasonRefId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.WrittenNoticeFile)
            .WithMany(f => f.TerminationNotices)
            .HasForeignKey(e => e.WrittenNoticeFileId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.DecidedByUser)
            .WithMany()
            .HasForeignKey(e => e.DecidedByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
