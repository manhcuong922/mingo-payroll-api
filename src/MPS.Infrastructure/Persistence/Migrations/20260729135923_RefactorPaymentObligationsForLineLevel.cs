using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPaymentObligationsForLineLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data migration: enum PaymentObligationStatus đổi ngữ nghĩa giữa v1 và v3.
            //   v1: Pending=1, PaidConfirmed=2, PaidWithDiscrepancy=3, Overdue=4
            //   v3: Pending=1, PartiallyPaid=2, Completed=3, Overdue=4
            // Cả 2 giá trị v1 (2 và 3) đều diễn tả "đã nộp" — map sang v3 Completed(3).
            // Pending(1) và Overdue(4) giữ nguyên.
            migrationBuilder.Sql("UPDATE [PaymentObligations] SET [Status] = 3 WHERE [Status] IN (2, 3);");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentObligations_Users_ConfirmedByUserId",
                table: "PaymentObligations");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentObligations_Users_RecordedByUserId",
                table: "PaymentObligations");

            migrationBuilder.DropIndex(
                name: "IX_PaymentObligations_ConfirmedByUserId",
                table: "PaymentObligations");

            migrationBuilder.DropColumn(
                name: "AuthorityConfirmationImageObjectKey",
                table: "PaymentObligations");

            migrationBuilder.DropColumn(
                name: "BankReceiptImageObjectKey",
                table: "PaymentObligations");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "PaymentObligations");

            migrationBuilder.DropColumn(
                name: "ConfirmedByUserId",
                table: "PaymentObligations");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "PaymentObligations");

            migrationBuilder.DropColumn(
                name: "PaidDate",
                table: "PaymentObligations");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "PaymentObligations");

            migrationBuilder.DropColumn(
                name: "TransactionRef",
                table: "PaymentObligations");

            migrationBuilder.RenameColumn(
                name: "RecordedByUserId",
                table: "PaymentObligations",
                newName: "CompletedByUserId");

            migrationBuilder.RenameColumn(
                name: "RecordedAt",
                table: "PaymentObligations",
                newName: "CompletedAt");

            migrationBuilder.RenameIndex(
                name: "IX_PaymentObligations_RecordedByUserId",
                table: "PaymentObligations",
                newName: "IX_PaymentObligations_CompletedByUserId");

            migrationBuilder.CreateTable(
                name: "PaymentObligationEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentObligationId = table.Column<int>(type: "int", nullable: false),
                    ObjectKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UploadedByUserId = table.Column<int>(type: "int", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentObligationEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentObligationEvidences_PaymentObligations_PaymentObligationId",
                        column: x => x.PaymentObligationId,
                        principalTable: "PaymentObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentObligationEvidences_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentObligationLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentObligationId = table.Column<int>(type: "int", nullable: false),
                    EmployeeInformationId = table.Column<int>(type: "int", nullable: false),
                    EmployeeCodeSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeFullNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmployeeContribution = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EmployerContribution = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false, computedColumnSql: "[EmployeeContribution] + [EmployerContribution]", stored: true),
                    LineStatus = table.Column<int>(type: "int", nullable: false),
                    PaidDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PaymentMethod = table.Column<int>(type: "int", nullable: true),
                    TransactionRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymentNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExcludedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExcludedByUserId = table.Column<int>(type: "int", nullable: true),
                    ExcludedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentObligationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentObligationLines_EmployeeInformations_EmployeeInformationId",
                        column: x => x.EmployeeInformationId,
                        principalTable: "EmployeeInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentObligationLines_PaymentObligations_PaymentObligationId",
                        column: x => x.PaymentObligationId,
                        principalTable: "PaymentObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentObligationLines_Users_ExcludedByUserId",
                        column: x => x.ExcludedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentObligationLines_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentObligationLines_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentObligationEvidences_ObjectKey",
                table: "PaymentObligationEvidences",
                column: "ObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentObligationEvidences_PaymentObligationId_UploadedAt",
                table: "PaymentObligationEvidences",
                columns: new[] { "PaymentObligationId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentObligationEvidences_UploadedByUserId",
                table: "PaymentObligationEvidences",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentObligationLines_EmployeeInformationId",
                table: "PaymentObligationLines",
                column: "EmployeeInformationId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentObligationLines_ExcludedByUserId",
                table: "PaymentObligationLines",
                column: "ExcludedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentObligationLines_PaymentObligationId_EmployeeInformationId",
                table: "PaymentObligationLines",
                columns: new[] { "PaymentObligationId", "EmployeeInformationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentObligationLines_PaymentObligationId_LineStatus",
                table: "PaymentObligationLines",
                columns: new[] { "PaymentObligationId", "LineStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentObligationLines_RecordedByUserId",
                table: "PaymentObligationLines",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentObligationLines_UpdatedByUserId",
                table: "PaymentObligationLines",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentObligations_Users_CompletedByUserId",
                table: "PaymentObligations",
                column: "CompletedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentObligations_Users_CompletedByUserId",
                table: "PaymentObligations");

            migrationBuilder.DropTable(
                name: "PaymentObligationEvidences");

            migrationBuilder.DropTable(
                name: "PaymentObligationLines");

            migrationBuilder.RenameColumn(
                name: "CompletedByUserId",
                table: "PaymentObligations",
                newName: "RecordedByUserId");

            migrationBuilder.RenameColumn(
                name: "CompletedAt",
                table: "PaymentObligations",
                newName: "RecordedAt");

            migrationBuilder.RenameIndex(
                name: "IX_PaymentObligations_CompletedByUserId",
                table: "PaymentObligations",
                newName: "IX_PaymentObligations_RecordedByUserId");

            migrationBuilder.AddColumn<string>(
                name: "AuthorityConfirmationImageObjectKey",
                table: "PaymentObligations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankReceiptImageObjectKey",
                table: "PaymentObligations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "PaymentObligations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmedByUserId",
                table: "PaymentObligations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "PaymentObligations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidDate",
                table: "PaymentObligations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "PaymentObligations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionRef",
                table: "PaymentObligations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentObligations_ConfirmedByUserId",
                table: "PaymentObligations",
                column: "ConfirmedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentObligations_Users_ConfirmedByUserId",
                table: "PaymentObligations",
                column: "ConfirmedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentObligations_Users_RecordedByUserId",
                table: "PaymentObligations",
                column: "RecordedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
