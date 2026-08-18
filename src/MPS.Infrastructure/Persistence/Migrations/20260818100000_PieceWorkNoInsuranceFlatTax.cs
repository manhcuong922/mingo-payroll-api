using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Phase 1 — đưa các HỢP ĐỒNG KHOÁN VIỆC đang có về đúng chế độ mới: không đóng
    /// BHXH/BHYT/BHTN + thuế khoán 10% (quyết định PO 18/08/2026).
    ///
    /// <para><c>ContractKindMetadata</c> chỉ quyết định lúc TẠO hợp đồng, nên các HĐ khoán ký trước
    /// bản sửa này vẫn giữ nguyên 4 giá trị HR tick tay — trong đó có thể có HĐ khoán bị khai đóng
    /// đủ BH và tính thuế lũy tiến.</para>
    ///
    /// <para><b>Chỉ áp cho HĐ CÒN HIỆU LỰC</b> (<c>Draft</c> / <c>Active</c> /
    /// <c>PendingTermination</c>). HĐ đã <c>Terminated</c> / <c>Expired</c> / <c>Cancelled</c> giữ
    /// nguyên để kỳ lương CŨ tính lại vẫn ra đúng số đã chốt — cùng nguyên tắc với
    /// <c>Contract.SalaryAmountSnapshot</c>.</para>
    ///
    /// <para>Không đụng schema; idempotent (điều kiện WHERE tự loại hàng đã đúng).</para>
    ///
    /// <para><c>Kind</c>, <c>Status</c> và <c>TaxPolicy</c> được lưu dạng CHUỖI tên enum
    /// (<c>HasConversion&lt;string&gt;</c> trong <c>ContractEntityConfiguration</c>), nên điều kiện
    /// dưới đây so chuỗi chứ không so số thứ tự.</para>
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260818100000_PieceWorkNoInsuranceFlatTax")]
    public partial class PieceWorkNoInsuranceFlatTax : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE Contracts
SET    SocialInsuranceFlag       = 0,
       HealthInsuranceFlag       = 0,
       UnemploymentInsuranceFlag = 0,
       TaxPolicy                 = N'FlatRate10Percent',
       UpdatedAt                 = SYSUTCDATETIME()
WHERE  Kind = N'PieceWork'
   AND [Status] IN (N'Draft', N'Active', N'PendingTermination')
   AND (SocialInsuranceFlag <> 0
     OR HealthInsuranceFlag <> 0
     OR UnemploymentInsuranceFlag <> 0
     OR TaxPolicy IS NULL
     OR TaxPolicy <> N'FlatRate10Percent');
");
        }

        /// <summary>
        /// Không rollback được: giá trị 4 trường trước khi vá không được lưu ở đâu cả. Việc vá cũng
        /// chỉ đưa dữ liệu về đúng chế độ đã chốt cho loại hợp đồng này.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
