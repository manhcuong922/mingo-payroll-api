using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Compliance;

namespace MPS.Infrastructure.Persistence.Configurations.Compliance;

public class LaborLawConstantsVersionEntityConfiguration : IEntityTypeConfiguration<LaborLawConstantsVersion>
{
    public void Configure(EntityTypeBuilder<LaborLawConstantsVersion> builder)
    {
        builder.ToTable("LaborLawConstantsVersions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.IsArchived).IsRequired().HasDefaultValue(false);

        builder.Property(e => e.ProbationSalaryMinPercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.OvertimeNightExtraPercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.NightWorkSurchargePercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.OvertimeNormalUpliftPercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.NightStartTime).HasColumnType("time").IsRequired();
        builder.Property(e => e.NightEndTime).HasColumnType("time").IsRequired();
        builder.Property(e => e.StandardWorkingHoursPerDay).HasColumnType("decimal(4,2)").IsRequired();
        builder.Property(e => e.MaxRegularHoursPerWeek).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.StandardWorkingDaysPerMonth).HasColumnType("decimal(4,2)").IsRequired();
        builder.Property(e => e.AnnualLeaveMinDays).IsRequired();
        builder.Property(e => e.WorkRateNormalFloor).HasColumnType("decimal(4,2)").IsRequired();
        builder.Property(e => e.WorkRateWeeklyRestFloor).HasColumnType("decimal(4,2)").IsRequired();
        builder.Property(e => e.WorkRateHolidayFloor).HasColumnType("decimal(4,2)").IsRequired();
        builder.Property(e => e.OvertimeMaxHoursPerDay).HasColumnType("decimal(4,2)").IsRequired();
        builder.Property(e => e.OvertimeMaxHoursPerMonth).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.OvertimeMaxHoursPerYear).IsRequired();

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
