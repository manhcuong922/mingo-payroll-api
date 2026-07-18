using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Evaluation;

namespace MPS.Infrastructure.Persistence.Configurations.Evaluation;

public class OrderEvaluationSubjectEntityConfiguration : IEntityTypeConfiguration<OrderEvaluationSubject>
{
    public void Configure(EntityTypeBuilder<OrderEvaluationSubject> b)
    {
        b.ToTable("OrderEvaluationSubjects");
        b.HasKey(x => x.Id);

        b.Property(x => x.FinalScore).HasPrecision(4, 2);
        b.Property(x => x.Note).HasMaxLength(2000);

        b.HasIndex(x => new { x.OrderEvaluationId, x.EmployeeUserId }).IsUnique();

        b.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Scores)
            .WithOne(s => s.OrderEvaluationSubject!)
            .HasForeignKey(s => s.OrderEvaluationSubjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
