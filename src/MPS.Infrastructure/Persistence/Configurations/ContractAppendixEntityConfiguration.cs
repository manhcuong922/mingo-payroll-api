using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class ContractAppendixEntityConfiguration : IEntityTypeConfiguration<ContractAppendix>
{
    public void Configure(EntityTypeBuilder<ContractAppendix> builder)
    {
        builder.ToTable("ContractAppendices");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.AppendixNo).HasMaxLength(50).IsRequired();
        builder.Property(e => e.AppendixType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(300).IsRequired();
        builder.Property(e => e.ChangedClausesJson).IsRequired();
        builder.Property(e => e.AppendixText).IsRequired();
        builder.Property(e => e.NoticeSentAt);
        builder.Property(e => e.EffectiveFrom).IsRequired();
        builder.Property(e => e.SignedAt);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasOne(e => e.Contract)
            .WithMany(c => c.ContractAppendices)
            .HasForeignKey(e => e.ContractId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
