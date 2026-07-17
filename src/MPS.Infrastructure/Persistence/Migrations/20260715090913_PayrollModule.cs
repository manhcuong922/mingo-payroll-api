using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PayrollModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollFormulaSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollFormulaSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollFormulaSets_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollFormulaSets_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollParameters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductivityCoefficients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinScore = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    MaxScore = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    Coefficient = table.Column<decimal>(type: "decimal(6,3)", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductivityCoefficients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployerPartyId = table.Column<int>(type: "int", nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<int>(type: "int", nullable: false),
                    PayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StandardWorkingDays = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FormulaSetId = table.Column<int>(type: "int", nullable: false),
                    FormulaSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParamsSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductivitySnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PitBracketsSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollPeriods_ContractParties_EmployerPartyId",
                        column: x => x.EmployerPartyId,
                        principalTable: "ContractParties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollPeriods_PayrollFormulaSets_FormulaSetId",
                        column: x => x.FormulaSetId,
                        principalTable: "PayrollFormulaSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollPeriods_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollPeriods_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalaryColumnDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormulaSetId = table.Column<int>(type: "int", nullable: false),
                    ColumnCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    DataType = table.Column<int>(type: "int", nullable: false),
                    ValueSource = table.Column<int>(type: "int", nullable: false),
                    LookupKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FormulaJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Rounding = table.Column<int>(type: "int", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsMoney = table.Column<bool>(type: "bit", nullable: false),
                    IsEditable = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryColumnDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryColumnDefinitions_PayrollFormulaSets_FormulaSetId",
                        column: x => x.FormulaSetId,
                        principalTable: "PayrollFormulaSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayrollComplianceRefs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollPeriodId = table.Column<int>(type: "int", nullable: false),
                    VersionType = table.Column<int>(type: "int", nullable: false),
                    VersionId = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    LegalReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ScopeId = table.Column<int>(type: "int", nullable: true),
                    HeadlineLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HeadlineValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollComplianceRefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollComplianceRefs_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayrollNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollPeriodId = table.Column<int>(type: "int", nullable: false),
                    NoteType = table.Column<int>(type: "int", nullable: false),
                    ColumnCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OldValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    NewValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    LegalReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VersionType = table.Column<int>(type: "int", nullable: true),
                    AutoGenerated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollNotes_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollNotes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollPeriodId = table.Column<int>(type: "int", nullable: false),
                    RowType = table.Column<int>(type: "int", nullable: false),
                    EmployeeInformationId = table.Column<int>(type: "int", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    InputSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollRows_EmployeeInformations_EmployeeInformationId",
                        column: x => x.EmployeeInformationId,
                        principalTable: "EmployeeInformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollRows_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayrollCellValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollRowId = table.Column<int>(type: "int", nullable: false),
                    ColumnCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ComputedValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TextValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OverrideValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    IsOverridden = table.Column<bool>(type: "bit", nullable: false),
                    OverriddenByUserId = table.Column<int>(type: "int", nullable: true),
                    OverriddenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExplanationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FormulaSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollCellValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollCellValues_PayrollRows_PayrollRowId",
                        column: x => x.PayrollRowId,
                        principalTable: "PayrollRows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollCellValues_Users_OverriddenByUserId",
                        column: x => x.OverriddenByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCellValues_OverriddenByUserId",
                table: "PayrollCellValues",
                column: "OverriddenByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCellValues_PayrollRowId_ColumnCode",
                table: "PayrollCellValues",
                columns: new[] { "PayrollRowId", "ColumnCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollComplianceRefs_PayrollPeriodId",
                table: "PayrollComplianceRefs",
                column: "PayrollPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollFormulaSets_CreatedByUserId",
                table: "PayrollFormulaSets",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollFormulaSets_IsDefault",
                table: "PayrollFormulaSets",
                column: "IsDefault",
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollFormulaSets_UpdatedByUserId",
                table: "PayrollFormulaSets",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollNotes_CreatedByUserId",
                table: "PayrollNotes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollNotes_PayrollPeriodId",
                table: "PayrollNotes",
                column: "PayrollPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollParameters_Key",
                table: "PayrollParameters",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_CreatedByUserId",
                table: "PayrollPeriods",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_EmployerPartyId_PeriodYear_PeriodMonth",
                table: "PayrollPeriods",
                columns: new[] { "EmployerPartyId", "PeriodYear", "PeriodMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_FormulaSetId",
                table: "PayrollPeriods",
                column: "FormulaSetId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_UpdatedByUserId",
                table: "PayrollPeriods",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRows_EmployeeInformationId",
                table: "PayrollRows",
                column: "EmployeeInformationId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRows_PayrollPeriodId_RowType",
                table: "PayrollRows",
                columns: new[] { "PayrollPeriodId", "RowType" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryColumnDefinitions_FormulaSetId_ColumnCode",
                table: "SalaryColumnDefinitions",
                columns: new[] { "FormulaSetId", "ColumnCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollCellValues");

            migrationBuilder.DropTable(
                name: "PayrollComplianceRefs");

            migrationBuilder.DropTable(
                name: "PayrollNotes");

            migrationBuilder.DropTable(
                name: "PayrollParameters");

            migrationBuilder.DropTable(
                name: "ProductivityCoefficients");

            migrationBuilder.DropTable(
                name: "SalaryColumnDefinitions");

            migrationBuilder.DropTable(
                name: "PayrollRows");

            migrationBuilder.DropTable(
                name: "PayrollPeriods");

            migrationBuilder.DropTable(
                name: "PayrollFormulaSets");
        }
    }
}
