using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Payroll.BhxhTax;

namespace MPS.Infrastructure.Persistence.Configurations.Payroll;

public class PaymentObligationLineEntityConfiguration : IEntityTypeConfiguration<PaymentObligationLine>
{
    public void Configure(EntityTypeBuilder<PaymentObligationLine> builder)
    {
        builder.ToTable("PaymentObligationLines");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.LineStatus).HasConversion<int>();
        builder.Property(e => e.PaymentMethod).HasConversion<int?>();

        builder.Property(e => e.EmployeeCodeSnapshot).HasMaxLength(50).IsRequired();
        builder.Property(e => e.EmployeeFullNameSnapshot).HasMaxLength(200).IsRequired();

        builder.Property(e => e.EmployeeContribution).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.EmployerContribution).HasColumnType("decimal(18,2)").IsRequired();

        // Computed persisted column — sum của 2 cột trên, dùng cho ORDER BY & index.
        builder.Property(e => e.TotalAmount)
            .HasColumnType("decimal(18,2)")
            .HasComputedColumnSql("[EmployeeContribution] + [EmployerContribution]", stored: true);

        builder.Property(e => e.PaidAmount).HasColumnType("decimal(18,2)");
        builder.Property(e => e.TransactionRef).HasMaxLength(100);
        builder.Property(e => e.PaymentNotes).HasMaxLength(500);
        builder.Property(e => e.ExcludedReason).HasMaxLength(500);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.PaymentObligation)
            .WithMany(o => o.Lines)
            .HasForeignKey(e => e.PaymentObligationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.EmployeeInformation)
            .WithMany()
            .HasForeignKey(e => e.EmployeeInformationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.RecordedByUser).WithMany().HasForeignKey(e => e.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ExcludedByUser).WithMany().HasForeignKey(e => e.ExcludedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UpdatedByUser).WithMany().HasForeignKey(e => e.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PaymentObligationId, e.EmployeeInformationId }).IsUnique();
        builder.HasIndex(e => new { e.PaymentObligationId, e.LineStatus });
    }
}
