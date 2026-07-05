using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Attendance;

namespace MPS.Infrastructure.Persistence.Configurations.Attendance;

public class ScheduleTemplateShiftEntityConfiguration : IEntityTypeConfiguration<ScheduleTemplateShift>
{
    public void Configure(EntityTypeBuilder<ScheduleTemplateShift> builder)
    {
        builder.ToTable("ScheduleTemplateShifts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.HasIndex(e => new { e.ScheduleTemplateId, e.ShiftTemplateId }).IsUnique();
        builder.HasIndex(e => e.ShiftTemplateId);

        builder.HasOne(e => e.ScheduleTemplate)
            .WithMany(e => e.Shifts)
            .HasForeignKey(e => e.ScheduleTemplateId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ShiftTemplate)
            .WithMany()
            .HasForeignKey(e => e.ShiftTemplateId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
