using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ComplianceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllowanceExemptThresholdVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AllowanceTypeId = table.Column<int>(type: "int", nullable: false),
                    ThresholdMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MonthlyExemptAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AppliesToTax = table.Column<bool>(type: "bit", nullable: false),
                    AppliesToSocial = table.Column<bool>(type: "bit", nullable: false),
                    LegalReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllowanceExemptThresholdVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllowanceExemptThresholdVersions_AllowanceTypes_AllowanceTypeId",
                        column: x => x.AllowanceTypeId,
                        principalTable: "AllowanceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MinimumWageVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegalReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinimumWageVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OtRateVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegalReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtRateVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PitRegulationVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PersonalDeductionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DependentDeductionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LegalReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PitRegulationVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SocialInsuranceRateVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EmployeeSocialPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EmployerSocialPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EmployerOccupationalDiseasePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EmployeeHealthPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EmployerHealthPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EmployeeUnemploymentPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EmployerUnemploymentPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EmployerUnionFundPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ReferenceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SocialCapMultiplier = table.Column<int>(type: "int", nullable: false, defaultValue: 20),
                    LegalReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialInsuranceRateVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MinimumWageRegions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinimumWageVersionId = table.Column<int>(type: "int", nullable: false),
                    RegionCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AmountPerMonth = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinimumWageRegions", x => x.Id);
                    table.CheckConstraint("CK_MinimumWageRegion_Amount", "[AmountPerMonth] > 0");
                    table.ForeignKey(
                        name: "FK_MinimumWageRegions_MinimumWageVersions_MinimumWageVersionId",
                        column: x => x.MinimumWageVersionId,
                        principalTable: "MinimumWageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OtRateEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OtRateVersionId = table.Column<int>(type: "int", nullable: false),
                    OtType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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

            migrationBuilder.CreateTable(
                name: "PitBrackets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PitRegulationVersionId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    LowerBound = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpperBound = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(5,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PitBrackets", x => x.Id);
                    table.CheckConstraint("CK_PitBracket_LowerBound", "[LowerBound] >= 0");
                    table.CheckConstraint("CK_PitBracket_Order", "[Order] >= 1");
                    table.CheckConstraint("CK_PitBracket_Rate", "[Rate] >= 0 AND [Rate] <= 1");
                    table.ForeignKey(
                        name: "FK_PitBrackets_PitRegulationVersions_PitRegulationVersionId",
                        column: x => x.PitRegulationVersionId,
                        principalTable: "PitRegulationVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceExemptThresholdVersions_AllowanceTypeId_ValidFrom",
                table: "AllowanceExemptThresholdVersions",
                columns: new[] { "AllowanceTypeId", "ValidFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceExemptThresholdVersions_AllowanceTypeId_ValidTo",
                table: "AllowanceExemptThresholdVersions",
                columns: new[] { "AllowanceTypeId", "ValidTo" },
                unique: true,
                filter: "[ValidTo] IS NULL AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MinimumWageRegions_MinimumWageVersionId_RegionCode",
                table: "MinimumWageRegions",
                columns: new[] { "MinimumWageVersionId", "RegionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MinimumWageVersions_ValidFrom",
                table: "MinimumWageVersions",
                column: "ValidFrom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MinimumWageVersions_ValidTo",
                table: "MinimumWageVersions",
                column: "ValidTo",
                unique: true,
                filter: "[ValidTo] IS NULL AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OtRateEntries_OtRateVersionId_OtType",
                table: "OtRateEntries",
                columns: new[] { "OtRateVersionId", "OtType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtRateVersions_ValidFrom",
                table: "OtRateVersions",
                column: "ValidFrom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtRateVersions_ValidTo",
                table: "OtRateVersions",
                column: "ValidTo",
                unique: true,
                filter: "[ValidTo] IS NULL AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PitBrackets_PitRegulationVersionId_Order",
                table: "PitBrackets",
                columns: new[] { "PitRegulationVersionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PitRegulationVersions_ValidFrom",
                table: "PitRegulationVersions",
                column: "ValidFrom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PitRegulationVersions_ValidTo",
                table: "PitRegulationVersions",
                column: "ValidTo",
                unique: true,
                filter: "[ValidTo] IS NULL AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SocialInsuranceRateVersions_ValidFrom",
                table: "SocialInsuranceRateVersions",
                column: "ValidFrom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialInsuranceRateVersions_ValidTo",
                table: "SocialInsuranceRateVersions",
                column: "ValidTo",
                unique: true,
                filter: "[ValidTo] IS NULL AND [IsArchived] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllowanceExemptThresholdVersions");

            migrationBuilder.DropTable(
                name: "MinimumWageRegions");

            migrationBuilder.DropTable(
                name: "OtRateEntries");

            migrationBuilder.DropTable(
                name: "PitBrackets");

            migrationBuilder.DropTable(
                name: "SocialInsuranceRateVersions");

            migrationBuilder.DropTable(
                name: "MinimumWageVersions");

            migrationBuilder.DropTable(
                name: "OtRateVersions");

            migrationBuilder.DropTable(
                name: "PitRegulationVersions");
        }
    }
}
