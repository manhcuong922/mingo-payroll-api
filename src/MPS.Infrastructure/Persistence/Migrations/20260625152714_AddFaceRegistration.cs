using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFaceRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            // ===== Seed permissions (IDs 134-136) — FaceRegistration (Track B) =====
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
                    // SystemAdmin (1)
                    { 134, 1 }, { 135, 1 }, { 136, 1 },
                    // HR (2)
                    { 134, 2 }, { 135, 2 }, { 136, 2 },
                    // Director (4): read-only
                    { 135, 4 },
                    // DepartmentManager (5): read-only
                    { 135, 5 },
                    // Employee (7): submit self + view own (self-service)
                    { 134, 7 },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FaceRegistrations");

            migrationBuilder.Sql("DELETE FROM RolePermissions WHERE PermissionId BETWEEN 134 AND 136;");
            migrationBuilder.Sql("DELETE FROM Permissions WHERE Id BETWEEN 134 AND 136;");
        }
    }
}
