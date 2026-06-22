using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Infrastructure.Persistence.Configurations.Attendance;

public class ScheduleTemplateEntityConfiguration : IEntityTypeConfiguration<ScheduleTemplate>
{
    public void Configure(EntityTypeBuilder<ScheduleTemplate> builder)
    {
        builder.ToTable("ScheduleTemplates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(e => e.Name).IsUnique();

        builder.Property(e => e.EffectiveFrom).IsRequired();
        builder.Property(e => e.EffectiveTo).IsRequired();

        builder.Property(e => e.RepeatUnit)
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(e => e.RepeatEvery).IsRequired();

        builder.Property(e => e.DaysOfWeekMask).HasMaxLength(50);
        builder.Property(e => e.DaysOfMonthMask).HasMaxLength(150);

        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);

        builder.HasOne(e => e.ShiftTemplate)
            .WithMany()
            .HasForeignKey(e => e.ShiftTemplateId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UpdatedByUser)
            .WithMany()
            .HasForeignKey(e => e.UpdatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Employees)
            .WithOne(e => e.ScheduleTemplate)
            .HasForeignKey(e => e.ScheduleTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
