using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;
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
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.IsArchived).IsRequired().HasDefaultValue(false);

        builder.Property(e => e.PersonalDeductionAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.DependentDeductionAmount).HasColumnType("decimal(18,2)").IsRequired();

        builder.Property(e => e.ShortTermContractMaxMonths).IsRequired().HasDefaultValue(3);
        builder.Property(e => e.ShortTermFlatRatePercent).HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(10.00m);
        builder.Property(e => e.ShortTermTaxExemptMonthlyIncome).HasColumnType("decimal(18,2)").IsRequired().HasDefaultValue(2_000_000m);
        builder.Property(e => e.HousingExemptRatePercent).HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(15.00m);

        builder.Property(e => e.LegalReference).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(2000);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired();
        builder.Property(e => e.UpdatedAt);
        builder.Property(e => e.UpdatedByUserId);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UpdatedByUser)
            .WithMany()
            .HasForeignKey(e => e.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.ValidFrom).IsUnique();
        builder.HasIndex(e => e.IsActive)
            .HasFilter("[IsActive] = 1 AND [IsArchived] = 0")
            .IsUnique();

        builder.HasMany(e => e.Brackets)
            .WithOne(b => b.PitRegulationVersion)
            .HasForeignKey(b => b.PitRegulationVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
