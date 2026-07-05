using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class TaskAssigneeEntityConfiguration : IEntityTypeConfiguration<TaskAssignee>
{
    public void Configure(EntityTypeBuilder<TaskAssignee> builder)
    {
        builder.ToTable("TaskAssignees");
        builder.HasKey(e => new { e.TaskId, e.UserId });   // 1 người/task không lặp

        builder.HasOne(a => a.Task)
            .WithMany(t => t.Assignees)
            .HasForeignKey(a => a.TaskId)
            .OnDelete(DeleteBehavior.Cascade);   // xóa task → xóa dòng gán

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.UserId);         // cho GetAssignedToUserAsync
    }
}
