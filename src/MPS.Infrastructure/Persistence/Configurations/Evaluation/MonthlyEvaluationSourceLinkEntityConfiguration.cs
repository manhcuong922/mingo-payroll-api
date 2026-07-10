using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Evaluation;

namespace MPS.Infrastructure.Persistence.Configurations.Evaluation;

public class MonthlyEvaluationSourceLinkEntityConfiguration : IEntityTypeConfiguration<MonthlyEvaluationSourceLink>
{
    public void Configure(EntityTypeBuilder<MonthlyEvaluationSourceLink> b)
    {
        b.ToTable("MonthlyEvaluationSourceLinks");
        b.HasKey(x => x.Id);

        b.Property(x => x.ContributedScore).HasPrecision(4, 2);

        b.HasIndex(x => x.OrderEvaluationSubjectId);

        // Restrict (không cascade) để tránh multiple-cascade-path: OrderEvaluationSubject đã cascade
        // từ OrderEvaluation; SourceLink chỉ cascade từ MonthlyEvaluation (cấu hình ở parent).
        b.HasOne(x => x.OrderEvaluationSubject)
            .WithMany()
            .HasForeignKey(x => x.OrderEvaluationSubjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
