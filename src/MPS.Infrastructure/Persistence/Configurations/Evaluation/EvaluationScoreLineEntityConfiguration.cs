using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Evaluation;

namespace MPS.Infrastructure.Persistence.Configurations.Evaluation;

public class EvaluationScoreLineEntityConfiguration : IEntityTypeConfiguration<EvaluationScoreLine>
{
    public void Configure(EntityTypeBuilder<EvaluationScoreLine> b)
    {
        b.ToTable("EvaluationScoreLines");
        b.HasKey(x => x.Id);

        b.Property(x => x.TypeNameSnapshot).HasMaxLength(200).IsRequired();
        b.Property(x => x.MetricNameSnapshot).HasMaxLength(200).IsRequired();
        b.Property(x => x.TypeWeightSnapshot).HasPrecision(6, 2);
        b.Property(x => x.MetricWeightSnapshot).HasPrecision(6, 2);
        b.Property(x => x.Note).HasMaxLength(2000);

        b.HasIndex(x => x.OrderEvaluationSubjectId);
        b.HasIndex(x => x.MonthlyEvaluationId);

        // Ref tiêu chí gốc — SetNull khi metric bị xóa (snapshot vẫn giữ). Owner FK
        // (OrderEvaluationSubjectId / MonthlyEvaluationId) cấu hình ở parent (cascade).
        b.HasOne(x => x.SourceMetric)
            .WithMany()
            .HasForeignKey(x => x.SourceMetricId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
