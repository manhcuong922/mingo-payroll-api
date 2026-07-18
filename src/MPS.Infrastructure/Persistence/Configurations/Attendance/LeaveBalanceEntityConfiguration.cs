using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Attendance;

namespace MPS.Infrastructure.Persistence.Configurations.Attendance;

public class LeaveBalanceEntityConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> builder)
    {
        builder.ToTable("LeaveBalances");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.EmployeeInformationId).IsRequired();
        builder.Property(e => e.Year).IsRequired();
        builder.Property(e => e.LeaveTypeId).IsRequired();
        builder.Property(e => e.BaseDays).HasColumnType("decimal(4,1)").IsRequired().HasDefaultValue(12m);
        builder.Property(e => e.SeniorityBonusDays).HasColumnType("decimal(4,1)").IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.TotalDays).HasColumnType("decimal(4,1)").IsRequired();
        builder.Property(e => e.UsedDays).HasColumnType("decimal(4,1)").IsRequired().HasDefaultValue(0m);

        builder.HasIndex(e => new { e.EmployeeInformationId, e.Year, e.LeaveTypeId }).IsUnique();

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);

        builder.HasOne(e => e.EmployeeInformation)
            .WithMany()
            .HasForeignKey(e => e.EmployeeInformationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.LeaveType)
            .WithMany()
            .HasForeignKey(e => e.LeaveTypeId)
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
