using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Attendance;

namespace MPS.Infrastructure.Persistence.Configurations.Attendance;

public class EmployeeShiftAssignmentEntityConfiguration : IEntityTypeConfiguration<EmployeeShiftAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeeShiftAssignment> builder)
    {
        builder.ToTable("EmployeeShiftAssignments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.EmployeeInformationId).IsRequired();
        builder.Property(e => e.ShiftTemplateId).IsRequired();
        builder.Property(e => e.WorkDate).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        // Multi-shift / ngày: 1 NV có thể có nhiều ca trên cùng ngày miễn không overlap thời gian.
        // Overlap được handler check (BatchUpdateShiftAssignmentsCommandHandler), DB chỉ chặn dup cùng ca.
        builder.HasIndex(e => new { e.EmployeeInformationId, e.WorkDate, e.ShiftTemplateId }).IsUnique();
        builder.HasIndex(e => e.WorkDate);

        builder.HasOne(e => e.EmployeeInformation)
            .WithMany()
            .HasForeignKey(e => e.EmployeeInformationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ShiftTemplate)
            .WithMany(s => s.EmployeeShiftAssignments)
            .HasForeignKey(e => e.ShiftTemplateId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.ScheduleTemplateId);
        builder.HasIndex(e => e.ScheduleTemplateId);

        builder.HasOne(e => e.ScheduleTemplate)
            .WithMany()
            .HasForeignKey(e => e.ScheduleTemplateId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}