using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Employee;

namespace MPS.Infrastructure.Persistence.Configurations;

public class EmployeeInformationEntityConfiguration : IEntityTypeConfiguration<EmployeeInformation>
{
    public void Configure(EntityTypeBuilder<EmployeeInformation> builder)
    {
        builder.ToTable("EmployeeInformations");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FullName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Dob).IsRequired();
        builder.Property(e => e.Gender).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(e => e.CitizenId).HasMaxLength(50).IsRequired();
        builder.Property(e => e.CitizenIssuedPlace).HasMaxLength(200);
        builder.Property(e => e.PermanentAddress).HasMaxLength(500).IsRequired();
        builder.Property(e => e.ContactAddress).HasMaxLength(500);
        builder.Property(e => e.Email).HasMaxLength(255);
        builder.Property(e => e.PhoneNo).HasMaxLength(20);
        builder.Property(e => e.EducationLevel).HasMaxLength(100);

        builder.Property(e => e.TaxNo).HasMaxLength(50);
        builder.Property(e => e.SocialNo).HasMaxLength(50);
        builder.Property(e => e.BankNumber).HasMaxLength(50);

        // Không đặt FK cứng sang BankCatalog/ProvinceCatalog: danh mục do ngân hàng phát hành,
        // có thể bị gỡ dòng khi ngân hàng cập nhật — khoá cứng sẽ chặn cả việc sửa danh mục.
        // Tính hợp lệ kiểm ở tầng validator lúc ghi hồ sơ.
        builder.Property(e => e.BankCode).HasMaxLength(50);
        builder.Property(e => e.BankBranch).HasMaxLength(200);
        builder.Property(e => e.BankProvinceCode).HasMaxLength(50);

        builder.Property(e => e.GuardianFullName).HasMaxLength(200);
        builder.Property(e => e.GuardianRelationship).HasMaxLength(50);
        builder.Property(e => e.GuardianPhoneNo).HasMaxLength(20);
        builder.Property(e => e.GuardianAddress).HasMaxLength(500);

        // 1 CCCD = 1 EI row
        builder.HasIndex(e => e.CitizenId).IsUnique();

        // 1 MST = 1 người (Điều 30 k.3 Luật QLT 2019), 1 sổ BHXH = 1 người (Điều 96 Luật BHXH 2014).
        // Lọc bỏ NULL và chuỗi rỗng: hai trường này là tuỳ chọn, rất nhiều hồ sơ chưa khai — nếu
        // index không lọc thì hồ sơ thứ hai chưa khai sẽ bị chặn oan.
        // Đây là lớp cuối chống race (hai request cùng lúc qua được validator); tầng ứng dụng vẫn
        // kiểm trước để trả 400 kèm thông báo thay vì để DB nổ 500.
        builder.HasIndex(e => e.TaxNo)
            .IsUnique()
            .HasFilter("[TaxNo] IS NOT NULL AND [TaxNo] <> ''");

        builder.HasIndex(e => e.SocialNo)
            .IsUnique()
            .HasFilter("[SocialNo] IS NOT NULL AND [SocialNo] <> ''");

        // Allow many rows with UserId=NULL, but when set UserId must be unique
        builder.HasIndex(e => e.UserId)
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");

        builder.HasOne(e => e.Position)
            .WithMany(p => p.EmployeeInformations)
            .HasForeignKey(e => e.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.User)
            .WithOne(u => u.EmployeeInformation)
            .HasForeignKey<EmployeeInformation>(e => e.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
