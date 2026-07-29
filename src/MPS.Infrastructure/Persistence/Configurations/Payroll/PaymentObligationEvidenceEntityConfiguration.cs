using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Payroll.BhxhTax;

namespace MPS.Infrastructure.Persistence.Configurations.Payroll;

public class PaymentObligationEvidenceEntityConfiguration : IEntityTypeConfiguration<PaymentObligationEvidence>
{
    public void Configure(EntityTypeBuilder<PaymentObligationEvidence> builder)
    {
        builder.ToTable("PaymentObligationEvidences");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ObjectKey).HasMaxLength(500).IsRequired();
        builder.Property(e => e.OriginalFileName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.UploadedAt).IsRequired();

        builder.HasOne(e => e.PaymentObligation)
            .WithMany(o => o.Evidences)
            .HasForeignKey(e => e.PaymentObligationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.UploadedByUser).WithMany().HasForeignKey(e => e.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.ObjectKey).IsUnique();
        builder.HasIndex(e => new { e.PaymentObligationId, e.UploadedAt });
    }
}
