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

/// <summary>
/// Bậc thang hỗ trợ theo tiến độ của HĐ khoán việc (Phase 2 — quyết định PO 18/08/2026).
/// Cùng khuôn với <see cref="ProductivityCoefficientEntityConfiguration"/>.
/// </summary>
public class PieceWorkSupportBandEntityConfiguration : IEntityTypeConfiguration<PieceWorkSupportBand>
{
    public void Configure(EntityTypeBuilder<PieceWorkSupportBand> builder)
    {
        builder.ToTable("PieceWorkSupportBands");
        builder.HasKey(e => e.Id);
        // Giờ/tháng: 3 chữ số phần nguyên là quá đủ (208h là trần thực tế), 2 số lẻ cho nửa giờ.
        builder.Property(e => e.MinHours).HasColumnType("decimal(6,2)").IsRequired();
        builder.Property(e => e.MaxHours).HasColumnType("decimal(6,2)");
        builder.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.Label).HasMaxLength(200);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => new { e.IsActive, e.MinHours });
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
