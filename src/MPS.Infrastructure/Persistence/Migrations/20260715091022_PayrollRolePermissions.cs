using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PayrollRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Payroll permissions (252–265).
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[,]
                {
                    { 252, "Read", "Payroll.Read.All", "Xem bảng lương mọi kỳ.", "Payroll", "All" },
                    { 253, "Read", "Payroll.Read.Self", "Xem phiếu lương của chính mình.", "Payroll", "Self" },
                    { 254, "Create", "Payroll.Create.All", "Sinh bảng lương tháng.", "Payroll", "All" },
                    { 255, "Update", "Payroll.Update.All", "Sửa tay ô / cột / ghi chú bảng lương.", "Payroll", "All" },
                    { 256, "Finalize", "Payroll.Finalize.All", "Chốt/khóa kỳ lương.", "Payroll", "All" },
                    { 257, "Delete", "Payroll.Delete.All", "Xoá kỳ lương nháp.", "Payroll", "All" },
                    { 258, "Read", "PayrollFormulaSet.Read.All", "Xem bảng công thức lương.", "PayrollFormulaSet", "All" },
                    { 259, "Create", "PayrollFormulaSet.Create.All", "Tạo bảng công thức lương.", "PayrollFormulaSet", "All" },
                    { 260, "Update", "PayrollFormulaSet.Update.All", "Sửa bảng/công thức cột.", "PayrollFormulaSet", "All" },
                    { 261, "Delete", "PayrollFormulaSet.Delete.All", "Xoá bảng công thức.", "PayrollFormulaSet", "All" },
                    { 262, "Read", "PayrollParameter.Read.All", "Xem tham số lương công ty.", "PayrollParameter", "All" },
                    { 263, "Update", "PayrollParameter.Update.All", "Sửa tham số lương công ty.", "PayrollParameter", "All" },
                    { 264, "Read", "ProductivityCoefficient.Read.All", "Xem bảng hệ số năng suất.", "ProductivityCoefficient", "All" },
                    { 265, "Update", "ProductivityCoefficient.Update.All", "Sửa bảng hệ số năng suất.", "ProductivityCoefficient", "All" }
                });

            // Gán role: SystemAdmin(1) full; Accountant(3) full (chủ sở hữu payroll);
            // Director(4) read-only; HR(2) xem bảng lương.
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 252, 1 }, { 253, 1 }, { 254, 1 }, { 255, 1 }, { 256, 1 }, { 257, 1 },
                    { 258, 1 }, { 259, 1 }, { 260, 1 }, { 261, 1 }, { 262, 1 }, { 263, 1 }, { 264, 1 }, { 265, 1 },

                    { 252, 3 }, { 253, 3 }, { 254, 3 }, { 255, 3 }, { 256, 3 }, { 257, 3 },
                    { 258, 3 }, { 259, 3 }, { 260, 3 }, { 261, 3 }, { 262, 3 }, { 263, 3 }, { 264, 3 }, { 265, 3 },

                    { 252, 4 }, { 258, 4 }, { 262, 4 }, { 264, 4 },

                    { 252, 2 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            for (var pid = 252; pid <= 265; pid++)
            {
                foreach (var rid in new[] { 1, 2, 3, 4 })
                {
                    migrationBuilder.DeleteData(
                        table: "RolePermissions",
                        keyColumns: new[] { "PermissionId", "RoleId" },
                        keyValues: new object[] { pid, rid });
                }
            }

            for (var pid = 252; pid <= 265; pid++)
            {
                migrationBuilder.DeleteData(table: "Permissions", keyColumn: "Id", keyValue: pid);
            }
        }
    }
}
