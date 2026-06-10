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
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.IsArchived).IsRequired().HasDefaultValue(false);

        builder.Property(e => e.AllowanceTypeId).IsRequired();
        // String thay enum — không redeploy khi luật bổ sung cơ chế tính mới
        builder.Property(e => e.ThresholdMode).HasMaxLength(30).IsRequired();
        builder.Property(e => e.MonthlyExemptAmount).HasColumnType("decimal(18,2)");
        builder.Property(e => e.AppliesToTax).IsRequired();
        builder.Property(e => e.AppliesToSocial).IsRequired();

        builder.Property(e => e.LegalReference).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(2000);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired();
        builder.Property(e => e.UpdatedAt);

        // Scoped per AllowanceType: chỉ 1 record open per AllowanceTypeId
        builder.HasIndex(e => new { e.AllowanceTypeId, e.ValidFrom }).IsUnique();
        builder.HasIndex(e => new { e.AllowanceTypeId, e.ValidTo })
            .HasFilter("[ValidTo] IS NULL AND [IsArchived] = 0")
            .IsUnique();

        builder.HasOne(e => e.AllowanceType)
            .WithMany()
            .HasForeignKey(e => e.AllowanceTypeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
