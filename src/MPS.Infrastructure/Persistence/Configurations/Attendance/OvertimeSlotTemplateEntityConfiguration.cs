using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Attendance;

namespace MPS.Infrastructure.Persistence.Configurations.Attendance;

public class OvertimeSlotTemplateEntityConfiguration : IEntityTypeConfiguration<OvertimeSlotTemplate>
{
    public void Configure(EntityTypeBuilder<OvertimeSlotTemplate> builder)
    {
        builder.ToTable("OvertimeSlotTemplates");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.Property(e => e.StartTime).HasColumnType("time").IsRequired();
        builder.Property(e => e.EndTime).HasColumnType("time").IsRequired();
        builder.Property(e => e.IsNightSlot).IsRequired();

        builder.Property(e => e.MultiplierNormalDay).HasColumnType("decimal(6,2)");
        builder.Property(e => e.MultiplierWeeklyRestDay).HasColumnType("decimal(6,2)");
        builder.Property(e => e.MultiplierHolidayDay).HasColumnType("decimal(6,2)");
        builder.Property(e => e.MultiplierNormalNightNoPrevOt).HasColumnType("decimal(6,2)");
        builder.Property(e => e.MultiplierNormalNightWithPrevOt).HasColumnType("decimal(6,2)");
        builder.Property(e => e.MultiplierWeeklyRestNight).HasColumnType("decimal(6,2)");
        builder.Property(e => e.MultiplierHolidayNight).HasColumnType("decimal(6,2)");

        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.IsActive);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired();
        builder.Property(e => e.UpdatedAt);
        builder.Property(e => e.UpdatedByUserId);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UpdatedByUser)
            .WithMany()
            .HasForeignKey(e => e.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
