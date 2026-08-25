using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Payroll.Banking;

namespace MPS.Infrastructure.Persistence.Configurations.Payroll;

public class CompanyPaymentSettingEntityConfiguration : IEntityTypeConfiguration<CompanyPaymentSetting>
{
    public void Configure(EntityTypeBuilder<CompanyPaymentSetting> builder)
    {
        builder.ToTable("CompanyPaymentSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.SalaryBankCode).HasMaxLength(50);
        builder.Property(e => e.SalaryBankBranch).HasMaxLength(200);
        builder.Property(e => e.SalaryAccountNo).HasMaxLength(50);
        builder.Property(e => e.SalaryAccountName).HasMaxLength(200);
        builder.Property(e => e.SalaryCustomerCif).HasMaxLength(50);
        builder.Property(e => e.SalaryContactPhone).HasMaxLength(20);

        builder.Property(e => e.BhxhUnitCode).HasMaxLength(50);
        builder.Property(e => e.BhxhAgencyCode).HasMaxLength(50);
        builder.Property(e => e.BhxhAgencyName).HasMaxLength(200);
        builder.Property(e => e.BhxhPayerBankCode).HasMaxLength(50);
        builder.Property(e => e.BhxhPayerBankBranch).HasMaxLength(200);
        builder.Property(e => e.BhxhPayerAccountNo).HasMaxLength(50);
        builder.Property(e => e.BhxhBeneficiaryAccountNo).HasMaxLength(50);
        builder.Property(e => e.BhxhBeneficiaryBankName).HasMaxLength(200);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.RowVersion).IsRowVersion();

        // Chốt singleton ở tầng DB: cột hằng + unique index ⇒ không thể chèn bản ghi thứ hai,
        // kể cả khi hai request cùng lúc vượt qua kiểm tra ở tầng ứng dụng. Nếu tồn tại 2 dòng
        // thì 2 chứng từ xuất cùng kỳ có thể mang số tài khoản khác nhau mà không ai phát hiện.
        builder.Property<byte>("SingletonGuard")
            .HasDefaultValue((byte)1)
            .IsRequired();
        builder.HasIndex("SingletonGuard").IsUnique();

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UpdatedByUser)
            .WithMany()
            .HasForeignKey(e => e.UpdatedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
