using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeaveRequestApproverRoute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "ApproverRoute",
                table: "LeaveRequests",
                type: "tinyint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_Status_ApproverRoute",
                table: "LeaveRequests",
                columns: new[] { "Status", "ApproverRoute" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_Status_ApproverRoute",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "ApproverRoute",
                table: "LeaveRequests");
        }
    }
}
