using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Attendance;

namespace MPS.Infrastructure.Persistence.Configurations.Attendance;

public class AttendanceSettingEntityConfiguration : IEntityTypeConfiguration<AttendanceSetting>
{
    public void Configure(EntityTypeBuilder<AttendanceSetting> builder)
    {
        builder.ToTable("AttendanceSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.IsEditLockEnabled).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.DailyEditCutoffTime).HasColumnType("time").IsRequired();
        builder.Property(e => e.LockAfterDays).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.AllowAdminOverride).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.RequireOverrideReason).IsRequired().HasDefaultValue(true);

        builder.Property(e => e.LateEarlyMaxOccurrencesPerMonth);
        builder.Property(e => e.LateEarlyMaxMinutesPerOccurrence);
        builder.Property(e => e.LateEarlyBackdateDaysAllowed);
        builder.Property(e => e.WfhMaxDaysPerMonth).HasColumnType("decimal(5,1)");
        builder.Property(e => e.WfhMinNoticeDays);
        builder.Property(e => e.WfhMaxDaysPerRequest).HasColumnType("decimal(5,1)");
        builder.Property(e => e.WfhWorkPlanMinLength);

        builder.Property(e => e.LateGraceMinutes);
        builder.Property(e => e.EarlyLeaveGraceMinutes);
        builder.Property(e => e.HalfDayThresholdMinutes);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedByUserId);
        builder.Property(e => e.UpdatedAt);
        builder.Property(e => e.UpdatedByUserId);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UpdatedByUser)
            .WithMany()
            .HasForeignKey(e => e.UpdatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
