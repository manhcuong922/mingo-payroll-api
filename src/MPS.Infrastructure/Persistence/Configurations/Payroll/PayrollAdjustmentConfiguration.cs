using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Payroll;

namespace MPS.Infrastructure.Persistence.Configurations.Payroll;

public class PayrollAdjustmentConfiguration : IEntityTypeConfiguration<PayrollAdjustment>
{
    public void Configure(EntityTypeBuilder<PayrollAdjustment> builder)
    {
        builder.ToTable("PayrollAdjustments");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasOne(e => e.EmployeeInformation).WithMany().HasForeignKey(e => e.EmployeeInformationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.SourcePeriod).WithMany().HasForeignKey(e => e.SourcePeriodId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AppliedPeriod).WithMany().HasForeignKey(e => e.AppliedPeriodId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.EmployeeInformationId, e.AppliedPeriodId })
            .HasDatabaseName("IX_PayrollAdjustments_Emp_AppliedPeriod");
    }
}
