using MPS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MPS.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Configuration cho PerformanceMetric (SQL Server).
/// Quan hệ nhiều-nhiều với Role qua bảng trung gian "PerformanceMetricRoles".
/// </summary>
public class PerformanceMetricEntityConfiguration : IEntityTypeConfiguration<PerformanceMetric>
{
    public void Configure(EntityTypeBuilder<PerformanceMetric> builder)
    {
        builder.ToTable("PerformanceMetrics");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.Weight)
            .HasPrecision(5, 2);

        builder.HasMany(e => e.TargetRoles)
            .WithMany()
            .UsingEntity(join => join.ToTable("PerformanceMetricRoles"));

        builder.HasIndex(e => e.Name);
    }
}
