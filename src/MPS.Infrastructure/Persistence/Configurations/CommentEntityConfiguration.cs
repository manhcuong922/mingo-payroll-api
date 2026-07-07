using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class CommentEntityConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.OwnerType).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Content).HasColumnType("nvarchar(max)").IsRequired();

        builder.HasOne(e => e.AuthorUser)
            .WithMany()
            .HasForeignKey(e => e.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Polymorphic (OwnerType + OwnerId) — KHÔNG FK cứng tới Project/Task, giống ProductionAttachment.
        builder.HasIndex(e => new { e.OwnerType, e.OwnerId });
    }
}
