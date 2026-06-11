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

        // DateOnly → SQL date (EF Core 8 tự map, không cần HasColumnType)
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.IsArchived).IsRequired().HasDefaultValue(false);

        builder.Property(e => e.EmployeeSocialPercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.EmployerSocialPercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.EmployerOccupationalDiseasePercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.EmployeeHealthPercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.EmployerHealthPercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.EmployeeUnemploymentPercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.EmployerUnemploymentPercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.EmployerUnionFundPercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.ReferenceAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.SocialCapMultiplier).IsRequired().HasDefaultValue(20);
        builder.Property(e => e.UnemploymentCapMultiplier).IsRequired().HasDefaultValue(20);

        builder.Property(e => e.LegalReference).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(2000);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired();
        builder.Property(e => e.UpdatedAt);
        builder.Property(e => e.UpdatedByUserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Chỉ 1 record open (ValidTo IS NULL) + chưa archived mỗi thời điểm
        builder.HasIndex(e => e.ValidFrom).IsUnique();
        builder.HasIndex(e => e.ValidTo)
            .HasFilter("[ValidTo] IS NULL AND [IsArchived] = 0")
            .IsUnique();
    }
}
