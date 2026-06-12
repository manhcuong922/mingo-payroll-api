using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ComplianceIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SocialInsuranceRateVersions_ValidTo",
                table: "SocialInsuranceRateVersions");

            migrationBuilder.DropIndex(
                name: "IX_PitRegulationVersions_ValidTo",
                table: "PitRegulationVersions");

            migrationBuilder.DropIndex(
                name: "IX_OtRateVersions_ValidTo",
                table: "OtRateVersions");

            migrationBuilder.DropIndex(
                name: "IX_MinimumWageVersions_ValidTo",
                table: "MinimumWageVersions");

            migrationBuilder.DropIndex(
                name: "IX_LaborLawConstantsVersions_ValidTo",
                table: "LaborLawConstantsVersions");

            migrationBuilder.DropIndex(
                name: "IX_AllowanceExemptThresholdVersions_AllowanceTypeId_ValidTo",
                table: "AllowanceExemptThresholdVersions");

            migrationBuilder.DropColumn(
                name: "ValidTo",
                table: "SocialInsuranceRateVersions");

            migrationBuilder.DropColumn(
                name: "ValidTo",
                table: "PitRegulationVersions");

            migrationBuilder.DropColumn(
                name: "ValidTo",
                table: "OtRateVersions");

            migrationBuilder.DropColumn(
                name: "ValidTo",
                table: "MinimumWageVersions");

            migrationBuilder.DropColumn(
                name: "ValidTo",
                table: "LaborLawConstantsVersions");

            migrationBuilder.DropColumn(
                name: "ValidTo",
                table: "AllowanceExemptThresholdVersions");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "SocialInsuranceRateVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "PitRegulationVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "OtRateVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "MinimumWageVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "LaborLawConstantsVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AllowanceExemptThresholdVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_SocialInsuranceRateVersions_IsActive",
                table: "SocialInsuranceRateVersions",
                column: "IsActive",
                unique: true,
                filter: "[IsActive] = 1 AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PitRegulationVersions_IsActive",
                table: "PitRegulationVersions",
                column: "IsActive",
                unique: true,
                filter: "[IsActive] = 1 AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OtRateVersions_IsActive",
                table: "OtRateVersions",
                column: "IsActive",
                unique: true,
                filter: "[IsActive] = 1 AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MinimumWageVersions_IsActive",
                table: "MinimumWageVersions",
                column: "IsActive",
                unique: true,
                filter: "[IsActive] = 1 AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LaborLawConstantsVersions_IsActive",
                table: "LaborLawConstantsVersions",
                column: "IsActive",
                unique: true,
                filter: "[IsActive] = 1 AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceExemptThresholdVersions_AllowanceTypeId_IsActive",
                table: "AllowanceExemptThresholdVersions",
                columns: new[] { "AllowanceTypeId", "IsActive" },
                unique: true,
                filter: "[IsActive] = 1 AND [IsArchived] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SocialInsuranceRateVersions_IsActive",
                table: "SocialInsuranceRateVersions");

            migrationBuilder.DropIndex(
                name: "IX_PitRegulationVersions_IsActive",
                table: "PitRegulationVersions");

            migrationBuilder.DropIndex(
                name: "IX_OtRateVersions_IsActive",
                table: "OtRateVersions");

            migrationBuilder.DropIndex(
                name: "IX_MinimumWageVersions_IsActive",
                table: "MinimumWageVersions");

            migrationBuilder.DropIndex(
                name: "IX_LaborLawConstantsVersions_IsActive",
                table: "LaborLawConstantsVersions");

            migrationBuilder.DropIndex(
                name: "IX_AllowanceExemptThresholdVersions_AllowanceTypeId_IsActive",
                table: "AllowanceExemptThresholdVersions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "SocialInsuranceRateVersions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "PitRegulationVersions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "OtRateVersions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "MinimumWageVersions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "LaborLawConstantsVersions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AllowanceExemptThresholdVersions");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidTo",
                table: "SocialInsuranceRateVersions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidTo",
                table: "PitRegulationVersions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidTo",
                table: "OtRateVersions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidTo",
                table: "MinimumWageVersions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidTo",
                table: "LaborLawConstantsVersions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidTo",
                table: "AllowanceExemptThresholdVersions",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialInsuranceRateVersions_ValidTo",
                table: "SocialInsuranceRateVersions",
                column: "ValidTo",
                unique: true,
                filter: "[ValidTo] IS NULL AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PitRegulationVersions_ValidTo",
                table: "PitRegulationVersions",
                column: "ValidTo",
                unique: true,
                filter: "[ValidTo] IS NULL AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OtRateVersions_ValidTo",
                table: "OtRateVersions",
                column: "ValidTo",
                unique: true,
                filter: "[ValidTo] IS NULL AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MinimumWageVersions_ValidTo",
                table: "MinimumWageVersions",
                column: "ValidTo",
                unique: true,
                filter: "[ValidTo] IS NULL AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LaborLawConstantsVersions_ValidTo",
                table: "LaborLawConstantsVersions",
                column: "ValidTo",
                unique: true,
                filter: "[ValidTo] IS NULL AND [IsArchived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceExemptThresholdVersions_AllowanceTypeId_ValidTo",
                table: "AllowanceExemptThresholdVersions",
                columns: new[] { "AllowanceTypeId", "ValidTo" },
                unique: true,
                filter: "[ValidTo] IS NULL AND [IsArchived] = 0");
        }
    }
}
