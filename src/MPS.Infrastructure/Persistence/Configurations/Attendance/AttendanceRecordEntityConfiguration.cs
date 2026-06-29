using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Attendance;

namespace MPS.Infrastructure.Persistence.Configurations.Attendance;

public class AttendanceRecordEntityConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecords");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.EmployeeInformationId).IsRequired();
        builder.Property(e => e.WorkDate).IsRequired();
        builder.Property(e => e.ShiftTemplateId).IsRequired();
        builder.Property(e => e.CheckInTime);
        builder.Property(e => e.CheckOutTime);
        builder.Property(e => e.DayHours).HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.NightHours).HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.Status).HasConversion<byte>().IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.OverrideReason).HasMaxLength(1000);

        // Multi-shift / ngày: 1 NV có thể có nhiều ca trên cùng ngày,
        // nhưng (emp, date, shift) phải unique để tránh tạo trùng record cùng ca.
        builder.HasIndex(e => new { e.EmployeeInformationId, e.WorkDate, e.ShiftTemplateId }).IsUnique();
        builder.HasIndex(e => e.WorkDate);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);

        builder.HasOne(e => e.EmployeeInformation)
            .WithMany()
            .HasForeignKey(e => e.EmployeeInformationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ShiftTemplate)
            .WithMany(s => s.AttendanceRecords)
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
    }
}