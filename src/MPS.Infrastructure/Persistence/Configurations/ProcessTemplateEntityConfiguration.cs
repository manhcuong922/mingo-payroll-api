using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class ProcessTemplateEntityConfiguration : IEntityTypeConfiguration<ProcessTemplate>
{
    public void Configure(EntityTypeBuilder<ProcessTemplate> builder)
    {
        builder.ToTable("ProcessTemplates");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique();
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Description).HasColumnType("nvarchar(max)");
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.BaseQuantity).HasDefaultValue(1);

        builder.HasOne(t => t.DefaultDepartment)
            .WithMany()
            .HasForeignKey(t => t.DefaultDepartmentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
