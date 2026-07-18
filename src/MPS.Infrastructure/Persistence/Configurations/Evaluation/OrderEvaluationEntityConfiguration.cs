using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Evaluation;

namespace MPS.Infrastructure.Persistence.Configurations.Evaluation;

public class OrderEvaluationEntityConfiguration : IEntityTypeConfiguration<OrderEvaluation>
{
    public void Configure(EntityTypeBuilder<OrderEvaluation> b)
    {
        b.ToTable("OrderEvaluations");
        b.HasKey(x => x.Id);

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => x.ProjectId).IsUnique();            // 1 đơn ↔ 1 bảng (idempotent, E8)
        b.HasIndex(x => new { x.PeriodYear, x.PeriodMonth });
        b.HasIndex(x => x.EvaluatorUserId);

        b.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Evaluator)
            .WithMany()
            .HasForeignKey(x => x.EvaluatorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Subjects)
            .WithOne(s => s.OrderEvaluation!)
            .HasForeignKey(s => s.OrderEvaluationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
