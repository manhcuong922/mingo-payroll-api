using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Employee;

namespace MPS.Infrastructure.Persistence.Configurations.Employee;

public class DependentRegistrationRequestEntityConfiguration : IEntityTypeConfiguration<DependentRegistrationRequest>
{
    public void Configure(EntityTypeBuilder<DependentRegistrationRequest> builder)
    {
        builder.ToTable("DependentRegistrationRequests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.EmployeeInformationId).IsRequired();
        builder.Property(e => e.RequestType).HasConversion<byte>().IsRequired();
        builder.Property(e => e.TargetDependentId);

        builder.Property(e => e.FullName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Gender).HasMaxLength(20);
        builder.Property(e => e.Relationship).HasConversion<byte>();
        builder.Property(e => e.TaxIdentificationNumber).HasMaxLength(13);
        builder.Property(e => e.CitizenId).HasMaxLength(12);
        builder.Property(e => e.IdentityDocumentType).HasConversion<byte?>();
        builder.Property(e => e.IdentityDocumentNumber).HasMaxLength(50);

        builder.Property(e => e.Status).HasConversion<byte>().IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.ReviewNote).HasMaxLength(500);
        builder.Property(e => e.CancelReason).HasMaxLength(500);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => new { e.EmployeeInformationId, e.Status });
        builder.HasIndex(e => e.Status);

        builder.HasOne(e => e.EmployeeInformation)
            .WithMany()
            .HasForeignKey(e => e.EmployeeInformationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TargetDependent)
            .WithMany()
            .HasForeignKey(e => e.TargetDependentId)
            .IsRequired(false)
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
