using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class ContractTerminationReasonRefEntityConfiguration : IEntityTypeConfiguration<ContractTerminationReasonRef>
{
    public void Configure(EntityTypeBuilder<ContractTerminationReasonRef> builder)
    {
        builder.ToTable("ContractTerminationReasonRefs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique();

        builder.Property(e => e.Description).HasMaxLength(500).IsRequired();
        builder.Property(e => e.RequiresWrittenNotice).IsRequired();
    }
}
