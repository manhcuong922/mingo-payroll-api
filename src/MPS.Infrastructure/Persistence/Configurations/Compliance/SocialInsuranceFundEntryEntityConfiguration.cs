using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Compliance;

namespace MPS.Infrastructure.Persistence.Configurations.Compliance;

public class SocialInsuranceFundEntryEntityConfiguration : IEntityTypeConfiguration<SocialInsuranceFundEntry>
{
    public void Configure(EntityTypeBuilder<SocialInsuranceFundEntry> builder)
    {
        builder.ToTable("SocialInsuranceFundEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.FundCode).HasMaxLength(50).IsRequired();
        builder.Property(e => e.FundName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.EmployeePercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(e => e.EmployerPercent).HasColumnType("decimal(5,2)").IsRequired();

        builder.HasOne(e => e.SocialInsuranceRateVersion)
            .WithMany(v => v.Funds)
            .HasForeignKey(e => e.SocialInsuranceRateVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SocialInsuranceRateVersionId, e.FundCode }).IsUnique();
    }
}
