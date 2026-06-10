using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Compliance;

namespace MPS.Infrastructure.Persistence.Configurations.Compliance;

public class OtRateEntryEntityConfiguration : IEntityTypeConfiguration<OtRateEntry>
{
    public void Configure(EntityTypeBuilder<OtRateEntry> builder)
    {
        builder.ToTable("OtRateEntries", t =>
        {
            t.HasCheckConstraint("CK_OtRateEntry_Rate", "[Rate] > 0 AND [Rate] <= 10");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.OtRateVersionId).IsRequired();
        // String thay enum — không redeploy khi luật thêm loại OT mới
        builder.Property(e => e.OtType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Label).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Rate).HasColumnType("decimal(5,2)").IsRequired();

        // Mỗi version chỉ có 1 entry per OtType
        builder.HasIndex(e => new { e.OtRateVersionId, e.OtType }).IsUnique();
    }
}
