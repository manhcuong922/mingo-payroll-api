using MPS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MPS.Infrastructure.Persistence.Configurations
{
    public class UserEntityConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");

            builder.HasKey(e => e.EmployeeCode);

            builder.Property(e => e.EmployeeCode)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(e => e.FullName)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(e => e.Username)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(e => e.Password)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(e => e.Email)
                .HasMaxLength(255);

            builder.Property(e => e.PhoneNo)
                .HasMaxLength(20);

            builder.HasOne(e => e.Role)
                .WithMany()
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
