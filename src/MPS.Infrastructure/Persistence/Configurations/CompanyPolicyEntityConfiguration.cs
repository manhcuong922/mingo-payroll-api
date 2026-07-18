using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class CompanyPolicyEntityConfiguration : IEntityTypeConfiguration<CompanyPolicy>
{
    public void Configure(EntityTypeBuilder<CompanyPolicy> builder)
    {
        builder.ToTable("CompanyPolicies");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Title).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);

        builder.HasIndex(e => e.IsActive);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UpdatedByUser)
            .WithMany()
            .HasForeignKey(e => e.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Files)
            .WithOne(f => f.CompanyPolicy)
            .HasForeignKey(f => f.CompanyPolicyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
