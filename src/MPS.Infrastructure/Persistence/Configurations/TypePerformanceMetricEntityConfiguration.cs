using MPS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MPS.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Configuration cho TypePerformanceMetric (SQL Server).
/// </summary>
public class TypePerformanceMetricEntityConfiguration : IEntityTypeConfiguration<TypePerformanceMetric>
{
    public void Configure(EntityTypeBuilder<TypePerformanceMetric> builder)
    {
        builder.ToTable("TypePerformanceMetrics");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Weight)
            .HasPrecision(5, 2);
    }
}
