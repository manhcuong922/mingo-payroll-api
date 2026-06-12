using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class SalaryGradeEntityConfiguration : IEntityTypeConfiguration<SalaryGrade>
{
    public void Configure(EntityTypeBuilder<SalaryGrade> builder)
    {
        builder.ToTable("SalaryGrades");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Level).IsRequired();
        builder.Property(e => e.BaseSalary).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired().HasDefaultValue("VND");
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);

        builder.HasOne(e => e.Position)
            .WithMany()
            .HasForeignKey(e => e.PositionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PositionId, e.Level })
            .IsUnique()
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("IX_SalaryGrades_PositionId_Level_Active");
    }
}
