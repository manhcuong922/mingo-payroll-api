using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Employee;

namespace MPS.Infrastructure.Persistence.Configurations;

public class EmployeeInformationEntityConfiguration : IEntityTypeConfiguration<EmployeeInformation>
{
    public void Configure(EntityTypeBuilder<EmployeeInformation> builder)
    {
        builder.ToTable("EmployeeInformations");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FullName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Dob).IsRequired();
        builder.Property(e => e.Gender).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(e => e.CitizenId).HasMaxLength(50).IsRequired();
        builder.Property(e => e.CitizenIssuedPlace).HasMaxLength(200);
        builder.Property(e => e.PermanentAddress).HasMaxLength(500).IsRequired();
        builder.Property(e => e.ContactAddress).HasMaxLength(500);
        builder.Property(e => e.Email).HasMaxLength(255);
        builder.Property(e => e.PhoneNo).HasMaxLength(20);
        builder.Property(e => e.EducationLevel).HasMaxLength(100);

        builder.Property(e => e.TaxNo).HasMaxLength(50);
        builder.Property(e => e.SocialNo).HasMaxLength(50);
        builder.Property(e => e.BankNumber).HasMaxLength(50);

        // 1 CCCD = 1 EI row
        builder.HasIndex(e => e.CitizenId).IsUnique();

        // Allow many rows with UserId=NULL, but when set UserId must be unique
        builder.HasIndex(e => e.UserId)
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");

        builder.HasOne(e => e.Position)
            .WithMany(p => p.EmployeeInformations)
            .HasForeignKey(e => e.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.User)
            .WithOne(u => u.EmployeeInformation)
            .HasForeignKey<EmployeeInformation>(e => e.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
