using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Attendance;

namespace MPS.Infrastructure.Persistence.Configurations.Attendance;

public class LateEarlyRequestEntityConfiguration : IEntityTypeConfiguration<LateEarlyRequest>
{
    public void Configure(EntityTypeBuilder<LateEarlyRequest> builder)
    {
        builder.ToTable("LateEarlyRequests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.EmployeeInformationId).IsRequired();
        builder.Property(e => e.WorkDate).IsRequired();
        builder.Property(e => e.Kind).HasConversion<byte>().IsRequired();
        builder.Property(e => e.Minutes).IsRequired();
        builder.Property(e => e.IsUnplanned).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.DeductFromLeaveBalance).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.ApproverRoute).HasConversion<byte>();
        builder.Property(e => e.Status).HasConversion<byte>().IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.ReviewNote).HasMaxLength(500);
        builder.Property(e => e.CancelReason).HasMaxLength(500);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);

        builder.HasIndex(e => new { e.EmployeeInformationId, e.WorkDate, e.Status });
        builder.HasIndex(e => new { e.Status, e.ApproverRoute });

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

        builder.HasOne(e => e.CancelledByUser)
            .WithMany()
            .HasForeignKey(e => e.CancelledByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UpdatedByUser)
            .WithMany()
            .HasForeignKey(e => e.UpdatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
