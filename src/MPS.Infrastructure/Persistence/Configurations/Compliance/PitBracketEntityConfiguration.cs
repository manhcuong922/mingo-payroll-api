using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Compliance;

namespace MPS.Infrastructure.Persistence.Configurations.Compliance;

public class PitBracketEntityConfiguration : IEntityTypeConfiguration<PitBracket>
{
    public void Configure(EntityTypeBuilder<PitBracket> builder)
    {
        builder.ToTable("PitBrackets", t =>
        {
            t.HasCheckConstraint("CK_PitBracket_Order", "[Order] >= 1");
            t.HasCheckConstraint("CK_PitBracket_LowerBound", "[LowerBound] >= 0");
            t.HasCheckConstraint("CK_PitBracket_Rate", "[Rate] >= 0 AND [Rate] <= 1");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PitRegulationVersionId).IsRequired();
        builder.Property(e => e.Order).IsRequired();
        builder.Property(e => e.LowerBound).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.UpperBound).HasColumnType("decimal(18,2)");
        // Rate decimal(5,4): vd 0.0500, 0.3500
        builder.Property(e => e.Rate).HasColumnType("decimal(5,4)").IsRequired();

        builder.HasIndex(e => new { e.PitRegulationVersionId, e.Order }).IsUnique();
    }
}
