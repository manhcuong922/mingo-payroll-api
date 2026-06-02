using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class ContractPartyEntityConfiguration : IEntityTypeConfiguration<ContractParty>
{
    public void Configure(EntityTypeBuilder<ContractParty> builder)
    {
        builder.ToTable("ContractParties");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.LegalName).HasMaxLength(300).IsRequired();
        builder.Property(e => e.TaxCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(500).IsRequired();
        builder.Property(e => e.RepresentativeFullName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.RepresentativeTitle).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EffectiveFrom).IsRequired();
        builder.Property(e => e.EffectiveTo);
        builder.Property(e => e.CreatedAt).IsRequired();
    }
}
