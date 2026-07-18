using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EvaluationModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonthlyEvaluations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeUserId = table.Column<int>(type: "int", nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<int>(type: "int", nullable: false),
                    SubjectRoleIdSnapshot = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FinalScore = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReopenedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReopenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReopenReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthlyEvaluations_Users_EmployeeUserId",
                        column: x => x.EmployeeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderEvaluations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    EvaluatorUserId = table.Column<int>(type: "int", nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderEvaluations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderEvaluations_Users_EvaluatorUserId",
                        column: x => x.EvaluatorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderEvaluationSubjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderEvaluationId = table.Column<int>(type: "int", nullable: false),
                    EmployeeUserId = table.Column<int>(type: "int", nullable: false),
                    SubjectRoleIdSnapshot = table.Column<int>(type: "int", nullable: false),
                    FinalScore = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderEvaluationSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderEvaluationSubjects_OrderEvaluations_OrderEvaluationId",
                        column: x => x.OrderEvaluationId,
                        principalTable: "OrderEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderEvaluationSubjects_Users_EmployeeUserId",
                        column: x => x.EmployeeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationScoreLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderEvaluationSubjectId = table.Column<int>(type: "int", nullable: true),
                    MonthlyEvaluationId = table.Column<int>(type: "int", nullable: true),
                    SourceMetricId = table.Column<int>(type: "int", nullable: true),
                    TypeNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MetricNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TypeWeightSnapshot = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    MetricWeightSnapshot = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Scale = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationScoreLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationScoreLines_MonthlyEvaluations_MonthlyEvaluationId",
                        column: x => x.MonthlyEvaluationId,
                        principalTable: "MonthlyEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvaluationScoreLines_OrderEvaluationSubjects_OrderEvaluationSubjectId",
                        column: x => x.OrderEvaluationSubjectId,
                        principalTable: "OrderEvaluationSubjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvaluationScoreLines_PerformanceMetrics_SourceMetricId",
                        column: x => x.SourceMetricId,
                        principalTable: "PerformanceMetrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyEvaluationSourceLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MonthlyEvaluationId = table.Column<int>(type: "int", nullable: false),
                    OrderEvaluationSubjectId = table.Column<int>(type: "int", nullable: false),
                    ContributedScore = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyEvaluationSourceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthlyEvaluationSourceLinks_MonthlyEvaluations_MonthlyEvaluationId",
                        column: x => x.MonthlyEvaluationId,
                        principalTable: "MonthlyEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MonthlyEvaluationSourceLinks_OrderEvaluationSubjects_OrderEvaluationSubjectId",
                        column: x => x.OrderEvaluationSubjectId,
                        principalTable: "OrderEvaluationSubjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[,]
                {
                    { 224, "Read", "OrderEvaluation.Read.Own", "Xem bảng đánh giá theo đơn mình phụ trách.", "OrderEvaluation", "Own" },
                    { 225, "Read", "OrderEvaluation.Read.All", "Xem mọi bảng đánh giá theo đơn.", "OrderEvaluation", "All" },
                    { 226, "Score", "OrderEvaluation.Score.Own", "Chấm điểm bảng đánh giá đơn mình phụ trách.", "OrderEvaluation", "Own" },
                    { 227, "Submit", "OrderEvaluation.Submit.Own", "Chốt (submit) bảng đánh giá theo đơn.", "OrderEvaluation", "Own" },
                    { 228, "Read", "MonthlyEvaluation.Read.All", "Xem mọi bảng đánh giá tháng.", "MonthlyEvaluation", "All" },
                    { 229, "Read", "MonthlyEvaluation.Read.Self", "Xem bảng đánh giá tháng của chính mình.", "MonthlyEvaluation", "Self" },
                    { 230, "Aggregate", "MonthlyEvaluation.Aggregate.All", "Tổng hợp bảng tháng từ các bảng theo đơn.", "MonthlyEvaluation", "All" },
                    { 231, "ScoreDirect", "MonthlyEvaluation.ScoreDirect.All", "Director chấm trực tiếp tổ trưởng/văn phòng.", "MonthlyEvaluation", "All" },
                    { 232, "Approve", "MonthlyEvaluation.Approve.All", "Duyệt & khóa bảng đánh giá tháng.", "MonthlyEvaluation", "All" },
                    { 233, "Reopen", "MonthlyEvaluation.Reopen.All", "Mở lại bảng tháng đã khóa.", "MonthlyEvaluation", "All" }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 224, 1 },
                    { 225, 1 },
                    { 226, 1 },
                    { 227, 1 },
                    { 228, 1 },
                    { 229, 1 },
                    { 230, 1 },
                    { 231, 1 },
                    { 232, 1 },
                    { 233, 1 },
                    { 225, 2 },
                    { 228, 2 },
                    { 229, 2 },
                    { 230, 2 },
                    { 232, 2 },
                    { 233, 2 },
                    { 229, 3 },
                    { 225, 4 },
                    { 228, 4 },
                    { 229, 4 },
                    { 231, 4 },
                    { 229, 5 },
                    { 224, 6 },
                    { 226, 6 },
                    { 227, 6 },
                    { 229, 6 },
                    { 229, 7 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationScoreLines_MonthlyEvaluationId",
                table: "EvaluationScoreLines",
                column: "MonthlyEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationScoreLines_OrderEvaluationSubjectId",
                table: "EvaluationScoreLines",
                column: "OrderEvaluationSubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationScoreLines_SourceMetricId",
                table: "EvaluationScoreLines",
                column: "SourceMetricId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyEvaluations_EmployeeUserId_PeriodYear_PeriodMonth",
                table: "MonthlyEvaluations",
                columns: new[] { "EmployeeUserId", "PeriodYear", "PeriodMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyEvaluations_PeriodYear_PeriodMonth",
                table: "MonthlyEvaluations",
                columns: new[] { "PeriodYear", "PeriodMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyEvaluationSourceLinks_MonthlyEvaluationId",
                table: "MonthlyEvaluationSourceLinks",
                column: "MonthlyEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyEvaluationSourceLinks_OrderEvaluationSubjectId",
                table: "MonthlyEvaluationSourceLinks",
                column: "OrderEvaluationSubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvaluations_EvaluatorUserId",
                table: "OrderEvaluations",
                column: "EvaluatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvaluations_PeriodYear_PeriodMonth",
                table: "OrderEvaluations",
                columns: new[] { "PeriodYear", "PeriodMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvaluations_ProjectId",
                table: "OrderEvaluations",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvaluationSubjects_EmployeeUserId",
                table: "OrderEvaluationSubjects",
                column: "EmployeeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvaluationSubjects_OrderEvaluationId_EmployeeUserId",
                table: "OrderEvaluationSubjects",
                columns: new[] { "OrderEvaluationId", "EmployeeUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvaluationScoreLines");

            migrationBuilder.DropTable(
                name: "MonthlyEvaluationSourceLinks");

            migrationBuilder.DropTable(
                name: "MonthlyEvaluations");

            migrationBuilder.DropTable(
                name: "OrderEvaluationSubjects");

            migrationBuilder.DropTable(
                name: "OrderEvaluations");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 224, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 225, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 226, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 227, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 228, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 229, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 230, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 231, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 232, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 233, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 225, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 228, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 229, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 230, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 232, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 233, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 229, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 225, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 228, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 229, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 231, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 229, 5 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 224, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 226, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 227, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 229, 6 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 229, 7 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 224);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 225);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 226);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 227);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 228);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 229);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 230);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 231);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 232);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 233);
        }
    }
}
