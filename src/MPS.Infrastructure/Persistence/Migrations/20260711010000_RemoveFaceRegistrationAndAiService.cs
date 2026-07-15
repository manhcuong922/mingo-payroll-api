using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFaceRegistrationAndAiService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Bỏ tính năng điểm danh bằng khuôn mặt (FaceRegistration) ──
            // Idempotent: vài DB dev đã bị drop bảng thủ công dù history vẫn ghi AddFaceRegistration.
            migrationBuilder.Sql(
                "IF OBJECT_ID(N'[FaceRegistrations]', N'U') IS NOT NULL DROP TABLE [FaceRegistrations];");

            // Permission FaceRegistration (Id 134–136) được seed thủ công bằng InsertData trong
            // migration AddFaceRegistration nên KHÔNG nằm trong model snapshot → phải xóa tường minh.
            migrationBuilder.Sql("DELETE FROM [RolePermissions] WHERE [PermissionId] BETWEEN 134 AND 136;");
            migrationBuilder.Sql("DELETE FROM [Permissions] WHERE [Id] BETWEEN 134 AND 136;");

            // ── Bỏ role AiService (chỉ phục vụ camera nhận diện khuôn mặt) ──
            // Role này tồn tại ở Id 8 trên DB migrate (AddAiServiceRole) nhưng Id 10 trong snapshot,
            // nên xóa theo Name để bao cả hai. Xóa tài khoản service (FK Users.RoleId → Roles.Id)
            // và mọi role-permission của nó TRƯỚC khi xóa role.
            migrationBuilder.Sql("DELETE FROM [Users] WHERE [Username] = 'ai-service';");
            migrationBuilder.Sql("DELETE FROM [RolePermissions] WHERE [RoleId] IN (SELECT [Id] FROM [Roles] WHERE [Name] = 'AiService');");
            migrationBuilder.Sql("DELETE FROM [Roles] WHERE [Name] = 'AiService';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Khôi phục role AiService (theo snapshot: Id 10). Tài khoản 'ai-service' sẽ được
            // DatabaseSeeder tạo lại lúc runtime nên không cần chèn ở đây.
            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAt", "Description", "IsSystem", "Name", "UpdatedAt" },
                values: new object[] { 10, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "AI Service account — chấm công và đọc embedding từ camera nhận diện khuôn mặt.", true, "AiService", null });

            // Khôi phục bảng FaceRegistrations.
            migrationBuilder.CreateTable(
                name: "FaceRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeInformationId = table.Column<int>(type: "int", nullable: false),
                    PhotoObjectKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EmbeddingJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RejectReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaceRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaceRegistrations_EmployeeInformations_EmployeeInformationId",
                        column: x => x.EmployeeInformationId,
                        principalTable: "EmployeeInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FaceRegistrations_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FaceRegistrations_EmployeeInformationId",
                table: "FaceRegistrations",
                column: "EmployeeInformationId");

            migrationBuilder.CreateIndex(
                name: "IX_FaceRegistrations_ReviewedByUserId",
                table: "FaceRegistrations",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FaceRegistrations_Status",
                table: "FaceRegistrations",
                column: "Status");

            // Khôi phục permission FaceRegistration (Id 134–136) + gán role (mirror AddFaceRegistration).
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[,]
                {
                    { 134, "Submit",  "FaceRegistration.Submit.Self", "Nộp ảnh đăng ký nhận diện khuôn mặt của bản thân.", "FaceRegistration", "Self" },
                    { 135, "Read",    "FaceRegistration.Read.All",    "Xem tất cả đăng ký nhận diện khuôn mặt.",           "FaceRegistration", "All"  },
                    { 136, "Approve", "FaceRegistration.Approve.All", "Duyệt / từ chối đăng ký nhận diện khuôn mặt.",     "FaceRegistration", "All"  },
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 134, 1 }, { 135, 1 }, { 136, 1 },
                    { 134, 2 }, { 135, 2 }, { 136, 2 },
                    { 135, 4 },
                    { 135, 5 },
                    { 134, 7 },
                });
        }
    }
}
