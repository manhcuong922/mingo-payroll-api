using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Backfill dữ liệu BHXH cho các version được seed bởi phiên bản CŨ (trước khi có bảng
    /// SocialInsuranceFundEntries + cột EligibilityMode). Những version đó bị rỗng Funds và
    /// EligibilityMode="" → UI trả funds:[] và payroll không tính được BHXH.
    ///
    /// IDEMPOTENT: chỉ chèn quỹ cho version CHƯA có quỹ nào, và chỉ set EligibilityMode cho
    /// version đang rỗng. KHÔNG đụng version đã có dữ liệu đúng. Chạy lại nhiều lần vẫn an toàn.
    ///
    /// Mức quỹ theo chuẩn VN (khớp DatabaseSeeder.CreateDefaultSocialInsuranceFunds):
    ///   HT      (Hưu trí, tử tuất):                 NLĐ 8%  / NSDLĐ 14%
    ///   OdTs    (Ốm đau, thai sản):                 NLĐ 0%  / NSDLĐ 3%
    ///   TnldBnn (Tai nạn lao động, bệnh nghề nghiệp): NLĐ 0%  / NSDLĐ 0.5%
    /// </summary>
    public partial class Compliance_BackfillSocialInsuranceFunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Chèn 3 quỹ cho MỌI version chưa có bất kỳ quỹ nào.
            //    Chống trùng theo (VersionId, FundCode) để an toàn khi chạy lại.
            migrationBuilder.Sql(@"
INSERT INTO [SocialInsuranceFundEntries] ([SocialInsuranceRateVersionId], [FundCode], [FundName], [EmployeePercent], [EmployerPercent])
SELECT v.[Id], f.[FundCode], f.[FundName], f.[EmployeePercent], f.[EmployerPercent]
FROM [SocialInsuranceRateVersions] v
CROSS JOIN (VALUES
    (N'HT',      N'Hưu trí, tử tuất',                        CAST(8.00 AS decimal(5,2)), CAST(14.00 AS decimal(5,2))),
    (N'OdTs',    N'Ốm đau, thai sản',                        CAST(0.00 AS decimal(5,2)), CAST(3.00  AS decimal(5,2))),
    (N'TnldBnn', N'Tai nạn lao động, bệnh nghề nghiệp',      CAST(0.00 AS decimal(5,2)), CAST(0.50  AS decimal(5,2)))
) AS f([FundCode], [FundName], [EmployeePercent], [EmployerPercent])
WHERE NOT EXISTS (
    SELECT 1 FROM [SocialInsuranceFundEntries] e
    WHERE e.[SocialInsuranceRateVersionId] = v.[Id]
)
AND NOT EXISTS (
    SELECT 1 FROM [SocialInsuranceFundEntries] e2
    WHERE e2.[SocialInsuranceRateVersionId] = v.[Id] AND e2.[FundCode] = f.[FundCode]
);
");

            // 2) Vá EligibilityMode cho version đang rỗng (NULL hoặc '').
            //    Mặc định ByContractSubtype + Probation,Official (khớp seeder). NOT NULL nên không để rỗng.
            migrationBuilder.Sql(@"
UPDATE [SocialInsuranceRateVersions]
SET [EligibilityMode] = N'ByContractSubtype',
    [EligibleContractSubtypesCsv] = CASE
        WHEN [EligibleContractSubtypesCsv] IS NULL OR LTRIM(RTRIM([EligibleContractSubtypesCsv])) = ''
        THEN N'Probation,Official'
        ELSE [EligibleContractSubtypesCsv]
    END
WHERE [EligibilityMode] IS NULL OR LTRIM(RTRIM([EligibilityMode])) = '';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không đảo ngược: đây là vá dữ liệu, không phân biệt được hàng backfill với hàng
            // người dùng tự nhập cùng FundCode. Rollback chỉ gỡ migration khỏi history.
        }
    }
}
