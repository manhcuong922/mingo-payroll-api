using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ⭐ Phase 6 — ô chứa ảnh phụ cấp NGAY TRƯỚC khi mỗi phụ lục áp thay đổi.
    ///
    /// <para><c>AppendixApplier</c> ghi đè thẳng <c>ContractAllowances</c> nên trạng thái cũ mất
    /// vĩnh viễn ⇒ tính lại một kỳ lương CŨ ăn phụ cấp MỚI, sai lịch sử. Cột này để collector tra
    /// ngược: phụ cấp tại ngày D = ảnh của phụ lục CŨ NHẤT có hiệu lực SAU ngày D.</para>
    ///
    /// <para>Chỉ thêm cột, KHÔNG backfill: phụ lục đã kích hoạt trước bản này không còn nguồn nào
    /// để dựng lại trạng thái cũ (dữ liệu đã bị ghi đè). Ô null ⇒ collector rơi về trạng thái hiện
    /// tại của hợp đồng, đúng bằng hành vi trước bản sửa.</para>
    /// </summary>
    public partial class AppendixAllowanceSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowanceSnapshotBeforeJson",
                table: "ContractAppendices",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowanceSnapshotBeforeJson",
                table: "ContractAppendices");
        }
    }
}
