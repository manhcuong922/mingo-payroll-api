using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Compliance_RemoveOtRate_AddLegalDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OtRateEntries");

            migrationBuilder.DropTable(
                name: "OtRateVersions");

            migrationBuilder.CreateTable(
                name: "LegalDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IssuedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IssuedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegalDocuments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalDocuments_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceVersionLegalDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComplianceEntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ComplianceVersionId = table.Column<int>(type: "int", nullable: false),
                    LegalDocumentId = table.Column<int>(type: "int", nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LinkedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceVersionLegalDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplianceVersionLegalDocuments_LegalDocuments_LegalDocumentId",
                        column: x => x.LegalDocumentId,
                        principalTable: "LegalDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComplianceVersionLegalDocuments_Users_LinkedByUserId",
                        column: x => x.LinkedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceVersionLegalDocuments_ComplianceEntityType_ComplianceVersionId_LegalDocumentId",
                table: "ComplianceVersionLegalDocuments",
                columns: new[] { "ComplianceEntityType", "ComplianceVersionId", "LegalDocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceVersionLegalDocuments_LegalDocumentId",
                table: "ComplianceVersionLegalDocuments",
                column: "LegalDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceVersionLegalDocuments_LinkedByUserId",
                table: "ComplianceVersionLegalDocuments",
                column: "LinkedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocuments_Code",
                table: "LegalDocuments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocuments_CreatedByUserId",
                table: "LegalDocuments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocuments_UpdatedByUserId",
                table: "LegalDocuments",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplianceVersionLegalDocuments");

            migrationBuilder.DropTable(
                name: "LegalDocuments");

            migrationBuilder.CreateTable(
                name: "OtRateVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegalReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtRateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtRateVersions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OtRateVersions_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OtRateEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OtRateVersionId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OtType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(5,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtRateEntries", x => x.Id);
                    table.CheckConstraint("CK_OtRateEntry_Rate", "[Rate] > 0 AND [Rate] <= 10");
                    table.ForeignKey(
                        name: "FK_OtRateEntries_OtRateVersions_OtRateVersionId",
                        column: x => x.OtRateVersionId,
                        principalTable: "OtRateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OtRateEntries_OtRateVersionId_OtType",
                table: "OtRateEntries",
                columns: new[] { "OtRateVersionId", "OtType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtRateVersions_CreatedByUserId",
                table: "OtRateVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OtRateVersions_IsActive",
                table: "OtRateVersions",
                column: "IsActive",
                unique: true,
                filter: "[IsActive] = 1 AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OtRateVersions_UpdatedByUserId",
                table: "OtRateVersions",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OtRateVersions_ValidFrom",
                table: "OtRateVersions",
                column: "ValidFrom",
                unique: true);
        }
    }
}
