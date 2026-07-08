using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Attendance;

namespace MPS.Infrastructure.Persistence.Configurations.Attendance;

public class OvertimeRequestEntityConfiguration : IEntityTypeConfiguration<OvertimeRequest>
{
    public void Configure(EntityTypeBuilder<OvertimeRequest> builder)
    {
        builder.ToTable("OvertimeRequests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.EmployeeInformationId).IsRequired();
        builder.Property(e => e.WorkDate).IsRequired();
        builder.Property(e => e.OtStartTime).IsRequired();
        builder.Property(e => e.OtEndTime).IsRequired();
        builder.Property(e => e.OtHours).HasColumnType("decimal(4,2)").IsRequired();
        builder.Property(e => e.OtDayHours).HasColumnType("decimal(4,2)").IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.OtNightHours).HasColumnType("decimal(4,2)").IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.OvertimeSlotTemplateId);
        builder.Property(e => e.Status).HasConversion<byte>().IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.ReviewNote).HasMaxLength(500);
        builder.Property(e => e.ReviewedAt);

        // Batch workflow — PM tạo cho N NV
        builder.Property(e => e.ProjectId);
        builder.Property(e => e.ApproverRoute).HasConversion<byte>();
        builder.Property(e => e.BatchId);

        builder.HasIndex(e => new { e.EmployeeInformationId, e.WorkDate, e.Status });
        builder.HasIndex(e => e.ProjectId);
        builder.HasIndex(e => e.BatchId);
        builder.HasIndex(e => new { e.Status, e.ApproverRoute });

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);

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

        builder.HasOne(e => e.OvertimeSlotTemplate)
            .WithMany(s => s.OvertimeRequests)
            .HasForeignKey(e => e.OvertimeSlotTemplateId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Project)
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}