using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Attendance;

namespace MPS.Infrastructure.Persistence.Configurations.Attendance;

public class AttendanceAdjustmentEntityConfiguration : IEntityTypeConfiguration<AttendanceAdjustment>
{
    public void Configure(EntityTypeBuilder<AttendanceAdjustment> builder)
    {
        builder.ToTable("AttendanceAdjustments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.AttendanceRecordId).IsRequired();
        builder.Property(e => e.EmployeeInformationId).IsRequired();
        builder.Property(e => e.NewStatus).HasConversion<byte>().IsRequired();
        builder.Property(e => e.NewCheckInTime);
        builder.Property(e => e.NewCheckOutTime);
        builder.Property(e => e.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.Status).HasConversion<byte>().IsRequired();
        builder.Property(e => e.ReviewNote).HasMaxLength(500);
        builder.Property(e => e.ReviewedAt);

        builder.Property(e => e.OriginalStatus).HasConversion<byte?>();
        builder.Property(e => e.OriginalCheckInTime);
        builder.Property(e => e.OriginalCheckOutTime);

        builder.HasIndex(e => e.AttendanceRecordId);
        builder.HasIndex(e => new { e.EmployeeInformationId, e.Status });

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);

        builder.HasOne(e => e.AttendanceRecord)
            .WithMany(r => r.AttendanceAdjustments)
            .HasForeignKey(e => e.AttendanceRecordId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.EmployeeInformation)
            .WithMany()
            .HasForeignKey(e => e.EmployeeInformationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ReviewedByUser)
            .WithMany()
            .HasForeignKey(e => e.ReviewedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UpdatedByUser)
            .WithMany()
            .HasForeignKey(e => e.UpdatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}