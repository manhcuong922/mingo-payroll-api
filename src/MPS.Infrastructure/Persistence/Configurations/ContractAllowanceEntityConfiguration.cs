using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class ContractAllowanceEntityConfiguration : IEntityTypeConfiguration<ContractAllowance>
{
    public void Configure(EntityTypeBuilder<ContractAllowance> builder)
    {
        builder.ToTable("ContractAllowances");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired().HasDefaultValue("VND");
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => new { e.ContractId, e.AllowanceTypeId }).IsUnique();
        builder.HasIndex(e => e.ContractId);

        builder.HasOne(e => e.Contract)
            .WithMany(c => c.ContractAllowances)
            .HasForeignKey(e => e.ContractId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.AllowanceType)
            .WithMany(a => a.ContractAllowances)
            .HasForeignKey(e => e.AllowanceTypeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
