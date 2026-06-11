using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Compliance;

namespace MPS.Infrastructure.Persistence.Configurations.Compliance;

public class MinimumWageVersionEntityConfiguration : IEntityTypeConfiguration<MinimumWageVersion>
{
    public void Configure(EntityTypeBuilder<MinimumWageVersion> builder)
    {
        builder.ToTable("MinimumWageVersions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.IsArchived).IsRequired().HasDefaultValue(false);

        builder.Property(e => e.LegalReference).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(2000);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired();
        builder.Property(e => e.UpdatedAt);
        builder.Property(e => e.UpdatedByUserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.ValidFrom).IsUnique();
        builder.HasIndex(e => e.ValidTo)
            .HasFilter("[ValidTo] IS NULL AND [IsArchived] = 0")
            .IsUnique();

        builder.HasMany(e => e.Regions)
            .WithOne(r => r.MinimumWageVersion)
            .HasForeignKey(r => r.MinimumWageVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
