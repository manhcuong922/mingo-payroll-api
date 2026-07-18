using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class ProjectEntityConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnType("nvarchar(max)");
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.StatusBeforePause).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.StartDate).IsRequired();

        builder.Property(e => e.CompletionNote).HasColumnType("nvarchar(max)");

        builder.HasOne(p => p.Department)
            .WithMany()
            .HasForeignKey(p => p.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);   // giữ lịch sử đơn — KHÔNG hard-delete xưởng

        // ⚠ KHÔNG còn FK Project → ProcessTemplate (bỏ 2026-07-03).

        builder.HasOne(p => p.CompletedBy)
            .WithMany()
            .HasForeignKey(p => p.CompletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.CreatedBy)
            .WithMany()
            .HasForeignKey(p => p.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ⭐ Tổ trưởng sản xuất chịu trách nhiệm đơn (refactor R/S) — nullable cho đơn cũ.
        builder.HasOne(p => p.ResponsibleUser)
            .WithMany()
            .HasForeignKey(p => p.ResponsibleUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.DepartmentId);
        builder.HasIndex(p => p.ResponsibleUserId);
        builder.HasIndex(p => p.DeletedAt);
    }
}
