using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class ProcessStepEntityConfiguration : IEntityTypeConfiguration<ProcessStep>
{
    public void Configure(EntityTypeBuilder<ProcessStep> builder)
    {
        builder.ToTable("ProcessSteps");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Description).HasColumnType("nvarchar(max)");
        builder.Property(e => e.DefaultProgressType).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(s => s.ProcessTemplate)
            .WithMany(t => t.Steps)
            .HasForeignKey(s => s.ProcessTemplateId)
            .OnDelete(DeleteBehavior.Cascade);   // xóa template → xóa các bước mẫu

        builder.HasIndex(s => new { s.ProcessTemplateId, s.StepOrder });
    }
}
