using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Employee;

namespace MPS.Infrastructure.Persistence.Configurations.Employee;

public class EmployeeDependentEntityConfiguration : IEntityTypeConfiguration<EmployeeDependent>
{
    public void Configure(EntityTypeBuilder<EmployeeDependent> builder)
    {
        builder.ToTable("EmployeeDependents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.EmployeeInformationId).IsRequired();
        builder.Property(e => e.FullName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.DateOfBirth).IsRequired();
        builder.Property(e => e.Gender).HasMaxLength(20);
        builder.Property(e => e.Relationship).HasConversion<byte>().IsRequired();
        builder.Property(e => e.TaxIdentificationNumber).HasMaxLength(13);
        builder.Property(e => e.CitizenId).HasMaxLength(12);
        builder.Property(e => e.RegisteredFromDate).IsRequired();
        builder.Property(e => e.RegisteredToDate);
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => new { e.EmployeeInformationId, e.IsActive });
        builder.HasIndex(e => e.CitizenId);
        builder.HasIndex(e => e.TaxIdentificationNumber);

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

        builder.HasOne(e => e.UpdatedByUser)
            .WithMany()
            .HasForeignKey(e => e.UpdatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
