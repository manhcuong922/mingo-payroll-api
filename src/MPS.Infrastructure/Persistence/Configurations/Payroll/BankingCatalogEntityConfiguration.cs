using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Payroll.Banking;
using MPS.Infrastructure.Persistence.SeedData;

namespace MPS.Infrastructure.Persistence.Configurations.Payroll;

public class BankCatalogEntityConfiguration : IEntityTypeConfiguration<BankCatalog>
{
    public void Configure(EntityTypeBuilder<BankCatalog> builder)
    {
        builder.ToTable("BankCatalogs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();
        builder.Property(e => e.FullName).HasMaxLength(200).IsRequired();

        // Mã viết tắt là thứ in thẳng lên lệnh chi — trùng mã thì không biết chọn dòng nào.
        builder.HasIndex(e => e.Code).IsUnique();

        builder.HasData(BankingCatalogSeedData.Banks.Select((b, i) => new BankCatalog
        {
            Id = i + 1,
            Code = b.Code,
            FullName = b.FullName,
            IsForeign = b.IsForeign,
            IsActive = true,
        }));
    }
}

public class ProvinceCatalogEntityConfiguration : IEntityTypeConfiguration<ProvinceCatalog>
{
    public void Configure(EntityTypeBuilder<ProvinceCatalog> builder)
    {
        builder.ToTable("ProvinceCatalogs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.FormName).HasMaxLength(100).IsRequired();

        builder.HasIndex(e => e.Code).IsUnique();

        builder.HasData(BankingCatalogSeedData.Provinces.Select((p, i) => new ProvinceCatalog
        {
            Id = i + 1,
            Code = p.Code,
            Name = p.Name,
            FormName = p.FormName,
            IsActive = true,
        }));
    }
}
