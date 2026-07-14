using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Attendance;

namespace MPS.Infrastructure.Persistence.Configurations.Attendance;

public class AttendanceRuleVersionEntityConfiguration : IEntityTypeConfiguration<AttendanceRuleVersion>
{
    public void Configure(EntityTypeBuilder<AttendanceRuleVersion> builder)
    {
        builder.ToTable("AttendanceRuleVersions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.EffectiveFrom).IsRequired();
        builder.Property(e => e.EffectiveTo);

        builder.Property(e => e.WorkRateNormal).HasColumnType("decimal(6,2)").IsRequired();
        builder.Property(e => e.WorkRateWeeklyRest).HasColumnType("decimal(6,2)").IsRequired();
        builder.Property(e => e.WorkRateHoliday).HasColumnType("decimal(6,2)").IsRequired();

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired();

        builder.HasOne(e => e.AttendanceRule)
            .WithMany(r => r.Versions)
            .HasForeignKey(e => e.AttendanceRuleId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Không cho 2 version cùng rule cùng ngày mở (chặn dup edge-case).
        builder.HasIndex(e => new { e.AttendanceRuleId, e.EffectiveFrom }).IsUnique();

        // Filtered unique: tại mỗi rule chỉ có tối đa 1 open version (EffectiveTo IS NULL).
        builder.HasIndex(e => e.AttendanceRuleId)
            .IsUnique()
            .HasFilter("[EffectiveTo] IS NULL")
            .HasDatabaseName("IX_AttendanceRuleVersions_OpenPerRule");
    }
}
