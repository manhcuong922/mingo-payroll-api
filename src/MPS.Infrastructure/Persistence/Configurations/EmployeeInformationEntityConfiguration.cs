using MPS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MPS.Infrastructure.Persistence.Configurations;

public class EmployeeInformationEntityConfiguration : IEntityTypeConfiguration<EmployeeInformation>
{
    public void Configure(EntityTypeBuilder<EmployeeInformation> builder)
    {
        builder.ToTable("EmployeeInformations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TaxNo).HasMaxLength(50);
        builder.Property(e => e.SocialNo).HasMaxLength(50);
        builder.Property(e => e.BankNumber).HasMaxLength(50);
        builder.Property(e => e.CitizenId).HasMaxLength(50);
        builder.Property(e => e.Address).HasMaxLength(500);

        builder.HasIndex(e => e.UserId).IsUnique();

        builder.HasOne(e => e.Position)
            .WithMany(p => p.EmployeeInformations)
            .HasForeignKey(e => e.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.User)
            .WithOne(u => u.EmployeeInformation)
            .HasForeignKey<EmployeeInformation>(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
