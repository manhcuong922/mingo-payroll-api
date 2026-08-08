using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Compliance;

namespace MPS.Infrastructure.Persistence.Configurations.Compliance;

public class SocialInsuranceRateVersionEntityConfiguration : IEntityTypeConfiguration<SocialInsuranceRateVersion>
{
    public void Configure(EntityTypeBuilder<SocialInsuranceRateVersion> builder)
    {
        builder.ToTable("SocialInsuranceRateVersions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.IsArchived).IsRequired().HasDefaultValue(false);

#pragma warning disable CS0618 // Obsolete — backward-compat columns, still need EF configuration
        builder.Property(e => e.EmployeeSocialPercent).HasColumnType("decimal(5,2)");
        builder.Property(e => e.EmployerSocialPercent).HasColumnType("decimal(5,2)");
        builder.Property(e => e.EmployerOccupationalDiseasePercent).HasColumnType("decimal(5,2)");
#pragma warning restore CS0618
        builder.Property(e => e.EmployeeHealthPercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.EmployerHealthPercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.EmployeeUnemploymentPercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.EmployerUnemploymentPercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.EmployerUnionFundPercent).HasColumnType("decimal(5,2)").IsRequired();
        // Bản ghi cũ (trước A4) chưa có 2 cột này — default = mức luật hiện hành QĐ 1908/QĐ-TLĐ
        // (1% lương, trần 10% lương cơ sở) để kỳ tính sau khi migrate ra đúng ngay.
        builder.Property(e => e.EmployeeUnionDuesPercent).HasColumnType("decimal(5,2)")
            .HasDefaultValue(1.00m).IsRequired();
        builder.Property(e => e.EmployeeUnionDuesCapPercent).HasColumnType("decimal(5,2)")
            .HasDefaultValue(10.00m).IsRequired();
        builder.Property(e => e.ReferenceAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.SocialCapMultiplier).IsRequired().HasDefaultValue(20);
        builder.Property(e => e.HealthCapMultiplier).IsRequired().HasDefaultValue(20);
        builder.Property(e => e.UnemploymentCapMultiplier).IsRequired().HasDefaultValue(20);

        builder.Property(e => e.EligibilityMode).HasMaxLength(30).IsRequired();
        builder.Property(e => e.EligibleContractSubtypesCsv).HasMaxLength(100);
        builder.Property(e => e.MinTenureMonths);

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
    }
}
