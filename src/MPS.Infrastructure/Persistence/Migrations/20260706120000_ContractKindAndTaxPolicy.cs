using MPS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260706120000_ContractKindAndTaxPolicy")]
    public partial class ContractKindAndTaxPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Add Kind as nullable first, then backfill, then make NOT NULL.
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Contracts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxPolicy",
                table: "Contracts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            // 2) Backfill Kind based on the legacy (Category, Subtype) tuple.
            //    - Subtype = 'Probation'                 → Kind = 'Probation'
            //    - Category ∈ {Definite, Indefinite}    → Kind = 'LaborFullTime'
            migrationBuilder.Sql("""
                UPDATE [Contracts]
                SET [Kind] = 'Probation'
                WHERE [ContractSubtype] = 'Probation';

                UPDATE [Contracts]
                SET [Kind] = 'LaborFullTime'
                WHERE [Kind] IS NULL;
                """);

            // 3) Enforce NOT NULL on Kind after backfill.
            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "Contracts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            // 4) Relax Category / Subtype to nullable — required for civil-code kinds
            //    (Service, PieceWork, Collaborator...) where "duration category" doesn't apply.
            migrationBuilder.AlterColumn<string>(
                name: "ContractCategory",
                table: "Contracts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "ContractSubtype",
                table: "Contracts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            // 5) Index for the common lookup pattern: employee × kind × status.
            migrationBuilder.CreateIndex(
                name: "IX_Contracts_EmployeeInformationId_Kind_Status",
                table: "Contracts",
                columns: new[] { "EmployeeInformationId", "Kind", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contracts_EmployeeInformationId_Kind_Status",
                table: "Contracts");

            // Revert nullability on Category / Subtype — backfill empty strings to satisfy NOT NULL.
            migrationBuilder.Sql("""
                UPDATE [Contracts] SET [ContractCategory] = 'Definite' WHERE [ContractCategory] IS NULL;
                UPDATE [Contracts] SET [ContractSubtype]  = 'Official' WHERE [ContractSubtype]  IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ContractSubtype",
                table: "Contracts",
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
                table: "Contracts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.DropColumn(name: "TaxPolicy", table: "Contracts");
            migrationBuilder.DropColumn(name: "Kind",       table: "Contracts");
        }
    }
}
