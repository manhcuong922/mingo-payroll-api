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
        // ShiftTemplateId nullable — NULL cho record OT (dùng OvertimeRequestId).
        builder.Property(e => e.ShiftTemplateId);
        builder.Property(e => e.OvertimeRequestId);
        builder.Property(e => e.CheckInAt).HasColumnType("datetime2");
        builder.Property(e => e.CheckOutAt).HasColumnType("datetime2");
        builder.Property(e => e.DayHours).HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.NightHours).HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.Status).HasConversion<byte>().IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.OverrideReason).HasMaxLength(1000);

        // Multi-shift / ngày: 1 NV có thể có nhiều ca trên cùng ngày,
        // nhưng (emp, date, shift) phải unique để tránh tạo trùng record cùng ca.
        // Filter WHERE ShiftTemplateId IS NOT NULL để record OT (ShiftTemplateId=null) không đụng.
        builder.HasIndex(e => new { e.EmployeeInformationId, e.WorkDate, e.ShiftTemplateId })
            .IsUnique()
            .HasFilter("[ShiftTemplateId] IS NOT NULL");

        // Record OT: 1 OvertimeRequest chỉ chấm được 1 lần (unique khi non-null).
        builder.HasIndex(e => e.OvertimeRequestId)
            .IsUnique()
            .HasFilter("[OvertimeRequestId] IS NOT NULL");

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
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.OvertimeRequest)
            .WithMany()
            .HasForeignKey(e => e.OvertimeRequestId)
            .IsRequired(false)
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
