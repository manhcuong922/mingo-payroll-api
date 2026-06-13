using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ComplianceAuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UnemploymentCapMultiplier",
                table: "SocialInsuranceRateVersions",
                type: "int",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "SocialInsuranceRateVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "PitRegulationVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "OtRateVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "MinimumWageVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "AllowanceExemptThresholdVersions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LaborLawConstantsVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ProbationSalaryMinPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    NightShiftSurchargePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    StandardWorkingHoursPerDay = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    StandardWorkingDaysPerMonth = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    AnnualLeaveMinDays = table.Column<int>(type: "int", nullable: false),
                    OvertimeMaxHoursPerDay = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    OvertimeMaxHoursPerYear = table.Column<int>(type: "int", nullable: false),
                    LegalReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaborLawConstantsVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaborLawConstantsVersions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LaborLawConstantsVersions_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SocialInsuranceRateVersions_CreatedByUserId",
                table: "SocialInsuranceRateVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SocialInsuranceRateVersions_UpdatedByUserId",
                table: "SocialInsuranceRateVersions",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PitRegulationVersions_CreatedByUserId",
                table: "PitRegulationVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PitRegulationVersions_UpdatedByUserId",
                table: "PitRegulationVersions",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OtRateVersions_CreatedByUserId",
                table: "OtRateVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OtRateVersions_UpdatedByUserId",
                table: "OtRateVersions",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MinimumWageVersions_CreatedByUserId",
                table: "MinimumWageVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MinimumWageVersions_UpdatedByUserId",
                table: "MinimumWageVersions",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceExemptThresholdVersions_CreatedByUserId",
                table: "AllowanceExemptThresholdVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceExemptThresholdVersions_UpdatedByUserId",
                table: "AllowanceExemptThresholdVersions",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LaborLawConstantsVersions_CreatedByUserId",
                table: "LaborLawConstantsVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LaborLawConstantsVersions_UpdatedByUserId",
                table: "LaborLawConstantsVersions",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LaborLawConstantsVersions_ValidFrom",
                table: "LaborLawConstantsVersions",
                column: "ValidFrom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LaborLawConstantsVersions_ValidTo",
                table: "LaborLawConstantsVersions",
                column: "ValidTo",
                unique: true,
                filter: "[ValidTo] IS NULL AND [IsArchived] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_AllowanceExemptThresholdVersions_Users_CreatedByUserId",
                table: "AllowanceExemptThresholdVersions",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AllowanceExemptThresholdVersions_Users_UpdatedByUserId",
                table: "AllowanceExemptThresholdVersions",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MinimumWageVersions_Users_CreatedByUserId",
                table: "MinimumWageVersions",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MinimumWageVersions_Users_UpdatedByUserId",
                table: "MinimumWageVersions",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OtRateVersions_Users_CreatedByUserId",
                table: "OtRateVersions",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OtRateVersions_Users_UpdatedByUserId",
                table: "OtRateVersions",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PitRegulationVersions_Users_CreatedByUserId",
                table: "PitRegulationVersions",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PitRegulationVersions_Users_UpdatedByUserId",
                table: "PitRegulationVersions",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SocialInsuranceRateVersions_Users_CreatedByUserId",
                table: "SocialInsuranceRateVersions",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SocialInsuranceRateVersions_Users_UpdatedByUserId",
                table: "SocialInsuranceRateVersions",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[,]
                {
                    { 65, "Read",   "SocialInsuranceRate.Read.All",   "Xem phiên bản tỷ lệ BHXH.", "SocialInsuranceRate", "All" },
                    { 66, "Create", "SocialInsuranceRate.Create.All", "Tạo phiên bản tỷ lệ BHXH mới.", "SocialInsuranceRate", "All" },
                    { 67, "Update", "SocialInsuranceRate.Update.All", "Cập nhật phiên bản BHXH chưa có hiệu lực.", "SocialInsuranceRate", "All" },
                    { 68, "Delete", "SocialInsuranceRate.Delete.All", "Archive phiên bản BHXH.", "SocialInsuranceRate", "All" },

                    { 69, "Read",   "PitRegulation.Read.All",   "Xem phiên bản quy định TNCN.", "PitRegulation", "All" },
                    { 70, "Create", "PitRegulation.Create.All", "Tạo phiên bản quy định TNCN mới.", "PitRegulation", "All" },
                    { 71, "Update", "PitRegulation.Update.All", "Cập nhật phiên bản TNCN chưa có hiệu lực.", "PitRegulation", "All" },
                    { 72, "Delete", "PitRegulation.Delete.All", "Archive phiên bản TNCN.", "PitRegulation", "All" },

                    { 73, "Read",   "OtRate.Read.All",   "Xem phiên bản hệ số OT.", "OtRate", "All" },
                    { 74, "Create", "OtRate.Create.All", "Tạo phiên bản hệ số OT mới.", "OtRate", "All" },
                    { 75, "Update", "OtRate.Update.All", "Cập nhật phiên bản OT chưa có hiệu lực.", "OtRate", "All" },
                    { 76, "Delete", "OtRate.Delete.All", "Archive phiên bản OT.", "OtRate", "All" },

                    { 77, "Read",   "MinimumWage.Read.All",   "Xem phiên bản lương tối thiểu vùng.", "MinimumWage", "All" },
                    { 78, "Create", "MinimumWage.Create.All", "Tạo phiên bản lương tối thiểu vùng mới.", "MinimumWage", "All" },
                    { 79, "Update", "MinimumWage.Update.All", "Cập nhật phiên bản lương tối thiểu chưa có hiệu lực.", "MinimumWage", "All" },
                    { 80, "Delete", "MinimumWage.Delete.All", "Archive phiên bản lương tối thiểu.", "MinimumWage", "All" },

                    { 81, "Read",   "AllowanceExemptThreshold.Read.All",   "Xem ngưỡng miễn phụ cấp.", "AllowanceExemptThreshold", "All" },
                    { 82, "Create", "AllowanceExemptThreshold.Create.All", "Tạo ngưỡng miễn phụ cấp mới.", "AllowanceExemptThreshold", "All" },
                    { 83, "Update", "AllowanceExemptThreshold.Update.All", "Cập nhật ngưỡng miễn chưa có hiệu lực.", "AllowanceExemptThreshold", "All" },
                    { 84, "Delete", "AllowanceExemptThreshold.Delete.All", "Archive ngưỡng miễn phụ cấp.", "AllowanceExemptThreshold", "All" },

                    { 85, "Read",   "LaborLawConstants.Read.All",   "Xem hằng số luật lao động.", "LaborLawConstants", "All" },
                    { 86, "Create", "LaborLawConstants.Create.All", "Tạo phiên bản hằng số luật lao động mới.", "LaborLawConstants", "All" },
                    { 87, "Update", "LaborLawConstants.Update.All", "Cập nhật hằng số luật lao động chưa có hiệu lực.", "LaborLawConstants", "All" },
                    { 88, "Delete", "LaborLawConstants.Delete.All", "Archive phiên bản hằng số luật lao động.", "LaborLawConstants", "All" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AllowanceExemptThresholdVersions_Users_CreatedByUserId",
                table: "AllowanceExemptThresholdVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_AllowanceExemptThresholdVersions_Users_UpdatedByUserId",
                table: "AllowanceExemptThresholdVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_MinimumWageVersions_Users_CreatedByUserId",
                table: "MinimumWageVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_MinimumWageVersions_Users_UpdatedByUserId",
                table: "MinimumWageVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_OtRateVersions_Users_CreatedByUserId",
                table: "OtRateVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_OtRateVersions_Users_UpdatedByUserId",
                table: "OtRateVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_PitRegulationVersions_Users_CreatedByUserId",
                table: "PitRegulationVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_PitRegulationVersions_Users_UpdatedByUserId",
                table: "PitRegulationVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_SocialInsuranceRateVersions_Users_CreatedByUserId",
                table: "SocialInsuranceRateVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_SocialInsuranceRateVersions_Users_UpdatedByUserId",
                table: "SocialInsuranceRateVersions");

            migrationBuilder.DropTable(
                name: "LaborLawConstantsVersions");

            migrationBuilder.DropIndex(
                name: "IX_SocialInsuranceRateVersions_CreatedByUserId",
                table: "SocialInsuranceRateVersions");

            migrationBuilder.DropIndex(
                name: "IX_SocialInsuranceRateVersions_UpdatedByUserId",
                table: "SocialInsuranceRateVersions");

            migrationBuilder.DropIndex(
                name: "IX_PitRegulationVersions_CreatedByUserId",
                table: "PitRegulationVersions");

            migrationBuilder.DropIndex(
                name: "IX_PitRegulationVersions_UpdatedByUserId",
                table: "PitRegulationVersions");

            migrationBuilder.DropIndex(
                name: "IX_OtRateVersions_CreatedByUserId",
                table: "OtRateVersions");

            migrationBuilder.DropIndex(
                name: "IX_OtRateVersions_UpdatedByUserId",
                table: "OtRateVersions");

            migrationBuilder.DropIndex(
                name: "IX_MinimumWageVersions_CreatedByUserId",
                table: "MinimumWageVersions");

            migrationBuilder.DropIndex(
                name: "IX_MinimumWageVersions_UpdatedByUserId",
                table: "MinimumWageVersions");

            migrationBuilder.DropIndex(
                name: "IX_AllowanceExemptThresholdVersions_CreatedByUserId",
                table: "AllowanceExemptThresholdVersions");

            migrationBuilder.DropIndex(
                name: "IX_AllowanceExemptThresholdVersions_UpdatedByUserId",
                table: "AllowanceExemptThresholdVersions");

            migrationBuilder.DropColumn(
                name: "UnemploymentCapMultiplier",
                table: "SocialInsuranceRateVersions");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "SocialInsuranceRateVersions");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "PitRegulationVersions");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "OtRateVersions");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "MinimumWageVersions");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "AllowanceExemptThresholdVersions");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValues: new object[] { 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88 });
        }
    }
}
