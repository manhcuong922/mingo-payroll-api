using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Attendance;

namespace MPS.Infrastructure.Persistence.Configurations.Attendance;

public class ScheduleTemplateEmployeeEntityConfiguration : IEntityTypeConfiguration<ScheduleTemplateEmployee>
{
    public void Configure(EntityTypeBuilder<ScheduleTemplateEmployee> builder)
    {
        builder.ToTable("ScheduleTemplateEmployees");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.HasIndex(e => new { e.ScheduleTemplateId, e.EmployeeInformationId }).IsUnique();

        builder.HasOne(e => e.EmployeeInformation)
            .WithMany()
            .HasForeignKey(e => e.EmployeeInformationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
