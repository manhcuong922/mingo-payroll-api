using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Payroll;

namespace MPS.Infrastructure.Persistence.Configurations.Payroll;

public class PayrollFormulaSetEntityConfiguration : IEntityTypeConfiguration<PayrollFormulaSet>
{
    public void Configure(EntityTypeBuilder<PayrollFormulaSet> builder)
    {
        builder.ToTable("PayrollFormulaSets");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.IsDefault).HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId)
            .IsRequired().OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UpdatedByUser).WithMany().HasForeignKey(e => e.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Columns).WithOne(c => c.FormulaSet).HasForeignKey(c => c.FormulaSetId)
            .OnDelete(DeleteBehavior.Cascade);

        // Chỉ 1 bảng mặc định.
        builder.HasIndex(e => e.IsDefault).HasFilter("[IsDefault] = 1").IsUnique();
    }
}

public class SalaryColumnDefinitionEntityConfiguration : IEntityTypeConfiguration<SalaryColumnDefinition>
{
    public void Configure(EntityTypeBuilder<SalaryColumnDefinition> builder)
    {
        builder.ToTable("SalaryColumnDefinitions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ColumnCode).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Label).HasMaxLength(200).IsRequired();
        builder.Property(e => e.GroupName).HasMaxLength(200);
        builder.Property(e => e.LookupKey).HasMaxLength(128);
        builder.Property(e => e.FormulaJson);
        builder.Property(e => e.DataType).HasConversion<int>();
        builder.Property(e => e.ValueSource).HasConversion<int>();
        builder.Property(e => e.Rounding).HasConversion<int>();
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => new { e.FormulaSetId, e.ColumnCode }).IsUnique();
    }
}

public class ProductivityCoefficientEntityConfiguration : IEntityTypeConfiguration<ProductivityCoefficient>
{
    public void Configure(EntityTypeBuilder<ProductivityCoefficient> builder)
    {
        builder.ToTable("ProductivityCoefficients");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MinScore).HasColumnType("decimal(4,2)").IsRequired();
        builder.Property(e => e.MaxScore).HasColumnType("decimal(4,2)").IsRequired();
        builder.Property(e => e.Coefficient).HasColumnType("decimal(6,3)").IsRequired();
        builder.Property(e => e.Label).HasMaxLength(200);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).IsRequired();
    }
}
