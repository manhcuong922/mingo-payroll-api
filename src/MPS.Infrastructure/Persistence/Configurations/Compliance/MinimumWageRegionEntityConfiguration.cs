using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Compliance;

namespace MPS.Infrastructure.Persistence.Configurations.Compliance;

public class MinimumWageRegionEntityConfiguration : IEntityTypeConfiguration<MinimumWageRegion>
{
    public void Configure(EntityTypeBuilder<MinimumWageRegion> builder)
    {
        builder.ToTable("MinimumWageRegions", t =>
        {
            t.HasCheckConstraint("CK_MinimumWageRegion_Amount", "[AmountPerMonth] > 0");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.MinimumWageVersionId).IsRequired();
        // String thay enum — không redeploy khi NĐ tái phân vùng
        builder.Property(e => e.RegionCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.AmountPerMonth).HasColumnType("decimal(18,2)").IsRequired();

        // Mỗi version chỉ có 1 region per RegionCode
        builder.HasIndex(e => new { e.MinimumWageVersionId, e.RegionCode }).IsUnique();
    }
}
