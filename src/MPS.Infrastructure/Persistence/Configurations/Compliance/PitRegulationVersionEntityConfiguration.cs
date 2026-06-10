using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Compliance;

namespace MPS.Infrastructure.Persistence.Configurations.Compliance;

public class PitRegulationVersionEntityConfiguration : IEntityTypeConfiguration<PitRegulationVersion>
{
    public void Configure(EntityTypeBuilder<PitRegulationVersion> builder)
    {
        builder.ToTable("PitRegulationVersions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.IsArchived).IsRequired().HasDefaultValue(false);

        builder.Property(e => e.PersonalDeductionAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.DependentDeductionAmount).HasColumnType("decimal(18,2)").IsRequired();

        builder.Property(e => e.LegalReference).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(2000);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired();
        builder.Property(e => e.UpdatedAt);

        builder.HasIndex(e => e.ValidFrom).IsUnique();
        builder.HasIndex(e => e.ValidTo)
            .HasFilter("[ValidTo] IS NULL AND [IsArchived] = 0")
            .IsUnique();

        builder.HasMany(e => e.Brackets)
            .WithOne(b => b.PitRegulationVersion)
            .HasForeignKey(b => b.PitRegulationVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
