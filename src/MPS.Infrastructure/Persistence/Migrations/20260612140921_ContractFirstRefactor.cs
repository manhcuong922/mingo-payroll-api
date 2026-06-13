using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContractFirstRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Users_EmployeeId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_SalaryGrades_Positions_PositionId",
                table: "SalaryGrades");

            migrationBuilder.DropIndex(
                name: "IX_SalaryGrades_Code",
                table: "SalaryGrades");

            migrationBuilder.DropIndex(
                name: "IX_SalaryGrades_PositionId",
                table: "SalaryGrades");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeInformations_UserId",
                table: "EmployeeInformations");

            migrationBuilder.DropColumn(
                name: "TemplateBody",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "ContractText",
                table: "Contracts");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "EmployeeInformations",
                newName: "PermanentAddress");

            migrationBuilder.RenameColumn(
                name: "EmployeeId",
                table: "Contracts",
                newName: "EmployeeInformationId");

            migrationBuilder.RenameIndex(
                name: "IX_Contracts_EmployeeId_Status",
                table: "Contracts",
                newName: "IX_Contracts_EmployeeInformationId_Status");

            migrationBuilder.AlterColumn<int>(
                name: "PositionId",
                table: "SalaryGrades",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Level",
                table: "SalaryGrades",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Positions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "EmployeeInformations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "TaxNo",
                table: "EmployeeInformations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "SocialNo",
                table: "EmployeeInformations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "BankNumber",
                table: "EmployeeInformations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<DateTime>(
                name: "CitizenIssuedDate",
                table: "EmployeeInformations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CitizenIssuedPlace",
                table: "EmployeeInformations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactAddress",
                table: "EmployeeInformations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Dob",
                table: "EmployeeInformations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "EducationLevel",
                table: "EmployeeInformations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "EmployeeInformations",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "EmployeeInformations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "EmployeeInformations",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhoneNo",
                table: "EmployeeInformations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TemplateFileId",
                table: "ContractTemplates",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmployerSignerUserId",
                table: "Contracts",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryGrades_PositionId_Level_Active",
                table: "SalaryGrades",
                columns: new[] { "PositionId", "Level" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.Sql("UPDATE Positions SET Code = 'POS' + CAST(Id AS NVARCHAR(10)) WHERE Code = ''");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_Code",
                table: "Positions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInformations_CitizenId",
                table: "EmployeeInformations",
                column: "CitizenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInformations_UserId",
                table: "EmployeeInformations",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTemplates_TemplateFileId",
                table: "ContractTemplates",
                column: "TemplateFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_EmployeeInformations_EmployeeInformationId",
                table: "Contracts",
                column: "EmployeeInformationId",
                principalTable: "EmployeeInformations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContractTemplates_ContractFiles_TemplateFileId",
                table: "ContractTemplates",
                column: "TemplateFileId",
                principalTable: "ContractFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SalaryGrades_Positions_PositionId",
                table: "SalaryGrades",
                column: "PositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_EmployeeInformations_EmployeeInformationId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_ContractTemplates_ContractFiles_TemplateFileId",
                table: "ContractTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_SalaryGrades_Positions_PositionId",
                table: "SalaryGrades");

            migrationBuilder.DropIndex(
                name: "IX_SalaryGrades_PositionId_Level_Active",
                table: "SalaryGrades");

            migrationBuilder.DropIndex(
                name: "IX_Positions_Code",
                table: "Positions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeInformations_CitizenId",
                table: "EmployeeInformations");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeInformations_UserId",
                table: "EmployeeInformations");

            migrationBuilder.DropIndex(
                name: "IX_ContractTemplates_TemplateFileId",
                table: "ContractTemplates");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "CitizenIssuedDate",
                table: "EmployeeInformations");

            migrationBuilder.DropColumn(
                name: "CitizenIssuedPlace",
                table: "EmployeeInformations");

            migrationBuilder.DropColumn(
                name: "ContactAddress",
                table: "EmployeeInformations");

            migrationBuilder.DropColumn(
                name: "Dob",
                table: "EmployeeInformations");

            migrationBuilder.DropColumn(
                name: "EducationLevel",
                table: "EmployeeInformations");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "EmployeeInformations");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "EmployeeInformations");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "EmployeeInformations");

            migrationBuilder.DropColumn(
                name: "PhoneNo",
                table: "EmployeeInformations");

            migrationBuilder.DropColumn(
                name: "TemplateFileId",
                table: "ContractTemplates");

            migrationBuilder.RenameColumn(
                name: "PermanentAddress",
                table: "EmployeeInformations",
                newName: "Address");

            migrationBuilder.RenameColumn(
                name: "EmployeeInformationId",
                table: "Contracts",
                newName: "EmployeeId");

            migrationBuilder.RenameIndex(
                name: "IX_Contracts_EmployeeInformationId_Status",
                table: "Contracts",
                newName: "IX_Contracts_EmployeeId_Status");

            migrationBuilder.AlterColumn<int>(
                name: "PositionId",
                table: "SalaryGrades",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "Level",
                table: "SalaryGrades",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "EmployeeInformations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TaxNo",
                table: "EmployeeInformations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SocialNo",
                table: "EmployeeInformations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BankNumber",
                table: "EmployeeInformations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateBody",
                table: "ContractTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "EmployerSignerUserId",
                table: "Contracts",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractText",
                table: "Contracts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryGrades_Code",
                table: "SalaryGrades",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryGrades_PositionId",
                table: "SalaryGrades",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInformations_UserId",
                table: "EmployeeInformations",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Users_EmployeeId",
                table: "Contracts",
                column: "EmployeeId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalaryGrades_Positions_PositionId",
                table: "SalaryGrades",
                column: "PositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
