using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ComplianceRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Gán 24 compliance permissions (65–88) vào RolePermissions
            // Theo spec mục 1.2:
            //   SystemAdmin (1) + HR (2): full CRUD (tất cả 24 perms)
            //   Accountant (3) + Director (4): Read-only (6 perms: 65,69,73,77,81,85)
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    // ── SystemAdmin (RoleId = 1) — full CRUD ──────────────────
                    { 65, 1 }, { 66, 1 }, { 67, 1 }, { 68, 1 },   // SocialInsuranceRate
                    { 69, 1 }, { 70, 1 }, { 71, 1 }, { 72, 1 },   // PitRegulation
                    { 73, 1 }, { 74, 1 }, { 75, 1 }, { 76, 1 },   // OtRate
                    { 77, 1 }, { 78, 1 }, { 79, 1 }, { 80, 1 },   // MinimumWage
                    { 81, 1 }, { 82, 1 }, { 83, 1 }, { 84, 1 },   // AllowanceExemptThreshold
                    { 85, 1 }, { 86, 1 }, { 87, 1 }, { 88, 1 },   // LaborLawConstants

                    // ── HR (RoleId = 2) — full CRUD (Primary Actor) ──────────
                    { 65, 2 }, { 66, 2 }, { 67, 2 }, { 68, 2 },
                    { 69, 2 }, { 70, 2 }, { 71, 2 }, { 72, 2 },
                    { 73, 2 }, { 74, 2 }, { 75, 2 }, { 76, 2 },
                    { 77, 2 }, { 78, 2 }, { 79, 2 }, { 80, 2 },
                    { 81, 2 }, { 82, 2 }, { 83, 2 }, { 84, 2 },
                    { 85, 2 }, { 86, 2 }, { 87, 2 }, { 88, 2 },

                    // ── Accountant (RoleId = 3) — Read-only ──────────────────
                    { 65, 3 }, { 69, 3 }, { 73, 3 },
                    { 77, 3 }, { 81, 3 }, { 85, 3 },

                    // ── Director (RoleId = 4) — Read-only ────────────────────
                    { 65, 4 }, { 69, 4 }, { 73, 4 },
                    { 77, 4 }, { 81, 4 }, { 85, 4 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 65, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 66, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 67, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 68, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 69, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 70, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 71, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 72, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 73, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 74, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 75, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 76, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 77, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 78, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 79, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 80, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 81, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 82, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 83, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 84, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 85, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 86, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 87, 1 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 88, 1 });

            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 65, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 66, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 67, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 68, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 69, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 70, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 71, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 72, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 73, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 74, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 75, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 76, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 77, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 78, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 79, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 80, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 81, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 82, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 83, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 84, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 85, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 86, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 87, 2 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 88, 2 });

            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 65, 3 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 69, 3 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 73, 3 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 77, 3 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 81, 3 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 85, 3 });

            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 65, 4 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 69, 4 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 73, 4 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 77, 4 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 81, 4 });
            migrationBuilder.DeleteData(table: "RolePermissions", keyColumns: new[] { "PermissionId", "RoleId" }, keyValues: new object[] { 85, 4 });
        }
    }
}
