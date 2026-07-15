using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Attendance;

namespace MPS.Infrastructure.Persistence.Configurations.Attendance;

public class AttendanceRuleEntityConfiguration : IEntityTypeConfiguration<AttendanceRule>
{
    public void Configure(EntityTypeBuilder<AttendanceRule> builder)
    {
        builder.ToTable("AttendanceRules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(e => e.Name)
            .IsUnique()
            .HasFilter("[IsActive] = 1");

        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);

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
