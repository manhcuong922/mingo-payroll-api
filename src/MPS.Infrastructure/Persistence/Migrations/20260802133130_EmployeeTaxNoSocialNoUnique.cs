using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 1 MST = 1 người (Điều 30 k.3 Luật QLT 2019), 1 sổ BHXH = 1 người (Điều 96 Luật BHXH 2014).
    ///
    /// <para><b>Lưu ý khi triển khai</b>: migration chạy TỰ ĐỘNG lúc khởi động
    /// (<c>db.Database.MigrateAsync()</c>). Nếu DB đang chạy đã có mã trùng thì việc tạo unique
    /// index sẽ thất bại và app không khởi động được. Vì "mã này thuộc về ai" là quyết định của
    /// con người — không được tự ý xoá mã của một trong hai — migration dừng lại kèm thông báo
    /// liệt kê đúng mã bị trùng, thay vì âm thầm huỷ dữ liệu thuế/BHXH của nhân viên.</para>
    /// </summary>
    public partial class EmployeeTaxNoSocialNoUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @dupTax NVARCHAR(MAX), @dupSocial NVARCHAR(MAX);

SELECT @dupTax = STRING_AGG(CONVERT(NVARCHAR(MAX), TaxNo), N', ')
FROM (SELECT TaxNo FROM [EmployeeInformations]
      WHERE TaxNo IS NOT NULL AND TaxNo <> ''
      GROUP BY TaxNo HAVING COUNT(*) > 1) AS t;

SELECT @dupSocial = STRING_AGG(CONVERT(NVARCHAR(MAX), SocialNo), N', ')
FROM (SELECT SocialNo FROM [EmployeeInformations]
      WHERE SocialNo IS NOT NULL AND SocialNo <> ''
      GROUP BY SocialNo HAVING COUNT(*) > 1) AS s;

IF @dupTax IS NOT NULL OR @dupSocial IS NOT NULL
BEGIN
    DECLARE @msg NVARCHAR(2000) =
        N'Khong the tao unique index: du lieu dang co ma trung. ' +
        ISNULL(N'MST trung: ' + @dupTax + N'. ', N'') +
        ISNULL(N'So so BHXH trung: ' + @dupSocial + N'. ', N'') +
        N'Moi ma chi thuoc ve MOT nguoi - hay xac dinh chu so huu that va xoa ma o cac ho so con lai, roi chay lai.';
    THROW 50001, @msg, 1;
END
");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInformations_SocialNo",
                table: "EmployeeInformations",
                column: "SocialNo",
                unique: true,
                filter: "[SocialNo] IS NOT NULL AND [SocialNo] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInformations_TaxNo",
                table: "EmployeeInformations",
                column: "TaxNo",
                unique: true,
                filter: "[TaxNo] IS NOT NULL AND [TaxNo] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmployeeInformations_SocialNo",
                table: "EmployeeInformations");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeInformations_TaxNo",
                table: "EmployeeInformations");
        }
    }
}
