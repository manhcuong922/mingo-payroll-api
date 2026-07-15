using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Evaluation;

namespace MPS.Infrastructure.Persistence.Configurations.Evaluation;

public class MonthlyEvaluationEntityConfiguration : IEntityTypeConfiguration<MonthlyEvaluation>
{
    public void Configure(EntityTypeBuilder<MonthlyEvaluation> b)
    {
        b.ToTable("MonthlyEvaluations");
        b.HasKey(x => x.Id);

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.FinalScore).HasPrecision(4, 2);
        b.Property(x => x.ReopenReason).HasMaxLength(2000);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => new { x.EmployeeUserId, x.PeriodYear, x.PeriodMonth }).IsUnique();   // D11
        b.HasIndex(x => new { x.PeriodYear, x.PeriodMonth });

        b.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.SourceLinks)
            .WithOne(l => l.MonthlyEvaluation!)
            .HasForeignKey(l => l.MonthlyEvaluationId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Scores)
            .WithOne(s => s.MonthlyEvaluation!)
            .HasForeignKey(s => s.MonthlyEvaluationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
