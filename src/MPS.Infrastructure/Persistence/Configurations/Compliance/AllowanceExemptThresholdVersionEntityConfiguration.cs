using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Compliance;

namespace MPS.Infrastructure.Persistence.Configurations.Compliance;

public class AllowanceExemptThresholdVersionEntityConfiguration : IEntityTypeConfiguration<AllowanceExemptThresholdVersion>
{
    public void Configure(EntityTypeBuilder<AllowanceExemptThresholdVersion> builder)
    {
        builder.ToTable("AllowanceExemptThresholdVersions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.IsArchived).IsRequired().HasDefaultValue(false);

        builder.Property(e => e.AllowanceTypeId).IsRequired();
        builder.Property(e => e.ThresholdMode).HasMaxLength(30).IsRequired();
        builder.Property(e => e.MonthlyExemptAmount).HasColumnType("decimal(18,2)");
        builder.Property(e => e.AppliesToTax).IsRequired();
        builder.Property(e => e.AppliesToSocial).IsRequired();

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

        builder.HasIndex(e => new { e.AllowanceTypeId, e.ValidFrom }).IsUnique();
        builder.HasIndex(e => new { e.AllowanceTypeId, e.IsActive })
            .HasFilter("[IsActive] = 1 AND [IsArchived] = 0")
            .IsUnique();

        builder.HasOne(e => e.AllowanceType)
            .WithMany()
            .HasForeignKey(e => e.AllowanceTypeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
