using MPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260706130000_ContractTemplateKind")]
    public partial class ContractTemplateKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Add Kind nullable then backfill then NOT NULL.
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "ContractTemplates",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            // 2) Backfill từ (Category, Subtype) cũ giống pattern Contracts.
            migrationBuilder.Sql("""
                UPDATE [ContractTemplates]
                SET [Kind] = 'Probation'
                WHERE [ContractSubtype] = 'Probation';

                UPDATE [ContractTemplates]
                SET [Kind] = 'LaborFullTime'
                WHERE [Kind] IS NULL;
                """);

            // 3) Enforce NOT NULL on Kind.
            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "ContractTemplates",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            // 4) Relax Category / Subtype to nullable.
            migrationBuilder.AlterColumn<string>(
                name: "ContractCategory",
                table: "ContractTemplates",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "ContractSubtype",
                table: "ContractTemplates",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            // 5) Index for common template-picker query: kind × active.
            migrationBuilder.CreateIndex(
                name: "IX_ContractTemplates_Kind_IsActive",
                table: "ContractTemplates",
                columns: new[] { "Kind", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContractTemplates_Kind_IsActive",
                table: "ContractTemplates");

            migrationBuilder.Sql("""
                UPDATE [ContractTemplates] SET [ContractCategory] = 'Definite' WHERE [ContractCategory] IS NULL;
                UPDATE [ContractTemplates] SET [ContractSubtype]  = 'Official' WHERE [ContractSubtype]  IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ContractSubtype",
                table: "ContractTemplates",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContractCategory",
                table: "ContractTemplates",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.DropColumn(name: "Kind", table: "ContractTemplates");
        }
    }
}
