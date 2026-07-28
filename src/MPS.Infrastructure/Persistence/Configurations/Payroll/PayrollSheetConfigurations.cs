using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Payroll;

namespace MPS.Infrastructure.Persistence.Configurations.Payroll;

public class PayrollPeriodEntityConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollPeriod> builder)
    {
        builder.ToTable("PayrollPeriods");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PayDate).IsRequired();
        builder.Property(e => e.StandardWorkingDays).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.StaleReason).HasMaxLength(500);
        builder.Property(e => e.FormulaSnapshotJson).IsRequired();
        builder.Property(e => e.ParamsSnapshotJson).IsRequired();
        builder.Property(e => e.ProductivitySnapshotJson).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.FormulaSet).WithMany().HasForeignKey(e => e.FormulaSetId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId)
            .IsRequired().OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UpdatedByUser).WithMany().HasForeignKey(e => e.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.ComplianceRefs).WithOne(r => r.PayrollPeriod).HasForeignKey(r => r.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Rows).WithOne(r => r.PayrollPeriod).HasForeignKey(r => r.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Notes).WithOne(n => n.PayrollPeriod).HasForeignKey(n => n.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1 công ty ⇒ mỗi tháng đúng 1 kỳ lương.
        builder.HasIndex(e => new { e.PeriodYear, e.PeriodMonth }).IsUnique();
    }
}

public class PayrollComplianceRefEntityConfiguration : IEntityTypeConfiguration<PayrollComplianceRef>
{
    public void Configure(EntityTypeBuilder<PayrollComplianceRef> builder)
    {
        builder.ToTable("PayrollComplianceRefs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.VersionType).HasConversion<int>();
        builder.Property(e => e.LegalReference).HasMaxLength(500);
        builder.Property(e => e.HeadlineLabel).HasMaxLength(200);
        builder.Property(e => e.HeadlineValue).HasColumnType("decimal(18,2)");
    }
}

public class PayrollRowEntityConfiguration : IEntityTypeConfiguration<PayrollRow>
{
    public void Configure(EntityTypeBuilder<PayrollRow> builder)
    {
        builder.ToTable("PayrollRows");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.RowType).HasConversion<int>();

        builder.HasOne(e => e.EmployeeInformation).WithMany().HasForeignKey(e => e.EmployeeInformationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Cells).WithOne(c => c.PayrollRow).HasForeignKey(c => c.PayrollRowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.PayrollPeriodId, e.RowType });
    }
}

public class PayrollCellValueEntityConfiguration : IEntityTypeConfiguration<PayrollCellValue>
{
    public void Configure(EntityTypeBuilder<PayrollCellValue> builder)
    {
        builder.ToTable("PayrollCellValues");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ColumnCode).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ComputedValue).HasColumnType("decimal(18,4)");
        builder.Property(e => e.OverrideValue).HasColumnType("decimal(18,4)");
        builder.Property(e => e.TextValue).HasMaxLength(500);
        builder.Property(e => e.ExplanationJson);
        builder.Property(e => e.FormulaSnapshotJson);

        builder.HasOne(e => e.OverriddenByUser).WithMany().HasForeignKey(e => e.OverriddenByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PayrollRowId, e.ColumnCode }).IsUnique();
    }
}

public class PayrollNoteEntityConfiguration : IEntityTypeConfiguration<PayrollNote>
{
    public void Configure(EntityTypeBuilder<PayrollNote> builder)
    {
        builder.ToTable("PayrollNotes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.NoteType).HasConversion<int>();
        builder.Property(e => e.VersionType).HasConversion<int>();
        builder.Property(e => e.ColumnCode).HasMaxLength(64);
        builder.Property(e => e.Title).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Body).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.LegalReference).HasMaxLength(500);
        builder.Property(e => e.OldValue).HasColumnType("decimal(18,4)");
        builder.Property(e => e.NewValue).HasColumnType("decimal(18,4)");
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
