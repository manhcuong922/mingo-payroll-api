using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// [D12] — mức lương mới do phụ lục hợp đồng đặt ra, đóng băng kèm mốc hiệu lực của phụ lục.
    ///
    /// Ghi chú: bản do <c>dotnet ef</c> sinh ra còn kèm 23 dòng <c>RolePermissions</c> (drift của
    /// model snapshot từ đợt sửa quyền nhóm tuân thủ). Đã bỏ vì các dòng đó ĐANG CÓ trong DB —
    /// chèn lại sẽ vi phạm khoá chính và chặn luôn migration này.
    /// </summary>
    public partial class AppendixSalarySnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SalaryAmountSnapshot",
                table: "ContractAppendices",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalaryCurrencySnapshot",
                table: "ContractAppendices",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SalaryAmountSnapshot",
                table: "ContractAppendices");

            migrationBuilder.DropColumn(
                name: "SalaryCurrencySnapshot",
                table: "ContractAppendices");
        }
    }
}
