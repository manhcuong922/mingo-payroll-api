using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Payroll.BhxhTax;

namespace MPS.Infrastructure.Persistence.Configurations.Payroll;

public class PaymentObligationEntityConfiguration : IEntityTypeConfiguration<PaymentObligation>
{
    public void Configure(EntityTypeBuilder<PaymentObligation> builder)
    {
        builder.ToTable("PaymentObligations");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ObligationType).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();

        builder.Property(e => e.AmountDue).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.Deadline).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(1000);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.PayrollPeriod).WithMany().HasForeignKey(e => e.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CompletedByUser).WithMany().HasForeignKey(e => e.CompletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UpdatedByUser).WithMany().HasForeignKey(e => e.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Mỗi kỳ chỉ có đúng 1 nghĩa vụ mỗi loại (idempotent guard cho re-lock).
        builder.HasIndex(e => new { e.PayrollPeriodId, e.ObligationType }).IsUnique();

        // Job daily scan: (Status, Deadline).
        builder.HasIndex(e => new { e.Status, e.Deadline });
    }
}
