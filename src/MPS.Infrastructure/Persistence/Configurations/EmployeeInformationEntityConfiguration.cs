using MPS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace MPS.Infrastructure.Persistence.Configurations
{
    public class EmployeeInformationEntityConfiguration : IEntityTypeConfiguration<EmployeeInformation>
    {
        public void Configure(EntityTypeBuilder<EmployeeInformation> builder)
        {
            builder.ToTable("EmployeeInformations");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.EmployeeCode)
                .HasMaxLength(50)
                .IsRequired();

            builder.HasOne(e => e.Position)
                .WithMany(p => p.EmployeeInformations)
                .HasForeignKey(e => e.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(e => e.User)
                .WithOne(u => u.EmployeeInformation)
                .HasForeignKey<EmployeeInformation>(e => e.EmployeeCode)
                .HasPrincipalKey<User>(u => u.EmployeeCode)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
