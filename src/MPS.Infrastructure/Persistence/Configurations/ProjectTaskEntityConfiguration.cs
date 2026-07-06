using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class ProjectTaskEntityConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable("ProjectTasks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnType("nvarchar(max)");
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.ProgressType).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.RowVersion).IsRowVersion();

        // Self-referencing (cây task)
        builder.HasOne(t => t.ParentTask)
            .WithMany(t => t.Children)
            .HasForeignKey(t => t.ParentTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        // Project (nullable)
        builder.HasOne(t => t.Project)
            .WithMany(p => p.Tasks)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);   // xóa đơn → xóa task của đơn

        // Department (xưởng phụ trách)
        builder.HasOne(t => t.Department)
            .WithMany()
            .HasForeignKey(t => t.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // CompletedBy (nullable — người bấm Complete)
        builder.HasOne(t => t.CompletedBy)
            .WithMany()
            .HasForeignKey(t => t.CompletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // CreatedBy
        builder.HasOne(t => t.CreatedBy)
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.ProjectId);
        builder.HasIndex(t => t.DepartmentId);
        builder.HasIndex(t => t.ParentTaskId);
        builder.HasIndex(t => t.Status);
    }
}
