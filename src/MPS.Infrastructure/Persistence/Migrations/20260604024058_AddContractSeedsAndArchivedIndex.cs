using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContractSeedsAndArchivedIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ContractTerminationReasonRefs",
                columns: new[] { "Id", "Code", "Description", "RequiresWrittenNotice" },
                values: new object[,]
                {
                    { 1, "ART34_C1_EXPIRY", "Hết hạn HĐLĐ (trừ k.4 Điều 177).", true },
                    { 2, "ART34_C2_WORK_COMPLETED", "Đã hoàn thành công việc theo HĐLĐ.", true },
                    { 3, "ART34_C3_MUTUAL_AGREEMENT", "Hai bên thỏa thuận chấm dứt HĐLĐ.", true },
                    { 4, "ART34_C4_CRIMINAL_SENTENCE", "NLĐ bị kết án tù không hưởng án treo / tử hình / cấm làm công việc.", false },
                    { 5, "ART34_C5_FOREIGNER_DEPORTED", "NLĐ là người nước ngoài bị trục xuất.", false },
                    { 6, "ART34_C6_EMPLOYEE_DEATH_OR_INCAPACITY", "NLĐ chết / Tòa tuyên mất năng lực hành vi / mất tích / đã chết.", false },
                    { 7, "ART34_C7_EMPLOYER_DEATH_OR_DISSOLVED", "NSDLĐ cá nhân chết / mất năng lực hành vi; tổ chức chấm dứt hoạt động.", false },
                    { 8, "ART34_C8_DISCIPLINARY_DISMISSAL", "NLĐ bị xử lý kỷ luật sa thải.", false },
                    { 9, "ART34_C9_EMPLOYEE_UNILATERAL", "NLĐ đơn phương chấm dứt HĐLĐ (Điều 35).", true },
                    { 10, "ART34_C10_EMPLOYER_UNILATERAL", "NSDLĐ đơn phương chấm dứt HĐLĐ (Điều 36).", true },
                    { 11, "ART34_C11_EMPLOYER_LAYOFF", "NSDLĐ cho NLĐ thôi việc (Điều 42, 43).", true },
                    { 12, "ART34_C12_WORK_PERMIT_EXPIRED", "Giấy phép lao động hết hiệu lực (Điều 156).", true },
                    { 13, "ART34_C13_PROBATION_FAILED", "Thỏa thuận thử việc không đạt / một bên hủy thỏa thuận thử việc.", true }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Code", "Description", "Resource", "Scope" },
                values: new object[,]
                {
                    { 33, "Read", "Contract.Read.All", "Xem tất cả HĐLĐ.", "Contract", "All" },
                    { 34, "Read", "Contract.Read.Self", "Xem HĐLĐ của chính mình.", "Contract", "Self" },
                    { 35, "Create", "Contract.Create.All", "Tạo HĐLĐ mới (DRAFT).", "Contract", "All" },
                    { 36, "Update", "Contract.Update.All", "Cập nhật HĐLĐ ở trạng thái DRAFT.", "Contract", "All" },
                    { 37, "Delete", "Contract.Delete.All", "Xóa HĐLĐ DRAFT.", "Contract", "All" },
                    { 38, "Activate", "Contract.Activate.All", "Upload PDF đã ký và kích hoạt HĐLĐ (DRAFT → ACTIVE).", "Contract", "All" },
                    { 39, "Cancel", "Contract.Cancel.All", "Hủy HĐLĐ DRAFT (DRAFT → CANCELLED).", "Contract", "All" },
                    { 40, "Read", "ContractAppendix.Read.All", "Xem phụ lục HĐLĐ của bất kỳ HĐ nào.", "ContractAppendix", "All" },
                    { 41, "Read", "ContractAppendix.Read.Self", "Xem phụ lục HĐLĐ của chính mình.", "ContractAppendix", "Self" },
                    { 42, "Create", "ContractAppendix.Create.All", "Tạo phụ lục HĐLĐ (DRAFT).", "ContractAppendix", "All" },
                    { 43, "Activate", "ContractAppendix.Activate.All", "Upload PDF phụ lục đã ký và áp dụng thay đổi.", "ContractAppendix", "All" },
                    { 44, "Read", "ContractTermination.Read.All", "Xem hồ sơ chấm dứt HĐLĐ của bất kỳ HĐ nào.", "ContractTermination", "All" },
                    { 45, "Read", "ContractTermination.Read.Self", "Xem hồ sơ chấm dứt HĐLĐ của chính mình.", "ContractTermination", "Self" },
                    { 46, "Create", "ContractTermination.Create.All", "Tạo hồ sơ chấm dứt HĐLĐ.", "ContractTermination", "All" },
                    { 47, "Read", "ContractFile.Read.All", "Tải file HĐ/phụ lục/thông báo của bất kỳ HĐ nào.", "ContractFile", "All" },
                    { 48, "Read", "ContractFile.Read.Self", "Tải file HĐ của chính mình.", "ContractFile", "Self" },
                    { 49, "Upload", "ContractFile.Upload.All", "Upload file PDF đã ký lên MinIO.", "ContractFile", "All" },
                    { 50, "Delete", "ContractFile.Delete.All", "Xóa file HĐ trên MinIO (chỉ SystemAdmin).", "ContractFile", "All" },
                    { 51, "Read", "SalaryGrade.Read.All", "Xem catalog bậc lương.", "SalaryGrade", "All" },
                    { 52, "Create", "SalaryGrade.Create.All", "Tạo bậc lương.", "SalaryGrade", "All" },
                    { 53, "Update", "SalaryGrade.Update.All", "Cập nhật bậc lương.", "SalaryGrade", "All" },
                    { 54, "Delete", "SalaryGrade.Delete.All", "Xóa bậc lương (soft nếu đã được HĐ tham chiếu).", "SalaryGrade", "All" },
                    { 55, "Read", "AllowanceType.Read.All", "Xem catalog phụ cấp.", "AllowanceType", "All" },
                    { 56, "Create", "AllowanceType.Create.All", "Tạo loại phụ cấp.", "AllowanceType", "All" },
                    { 57, "Update", "AllowanceType.Update.All", "Cập nhật loại phụ cấp.", "AllowanceType", "All" },
                    { 58, "Delete", "AllowanceType.Delete.All", "Xóa loại phụ cấp (soft nếu đã được HĐ tham chiếu).", "AllowanceType", "All" },
                    { 59, "Read", "ContractTemplate.Read.All", "Xem mẫu HĐLĐ.", "ContractTemplate", "All" },
                    { 60, "Create", "ContractTemplate.Create.All", "Tạo mẫu HĐLĐ.", "ContractTemplate", "All" },
                    { 61, "Update", "ContractTemplate.Update.All", "Cập nhật mẫu HĐLĐ (bump Version).", "ContractTemplate", "All" },
                    { 62, "Delete", "ContractTemplate.Delete.All", "Xóa mẫu HĐLĐ (soft).", "ContractTemplate", "All" },
                    { 63, "Read", "ContractParty.Read.All", "Xem pháp nhân NSDLĐ.", "ContractParty", "All" },
                    { 64, "Create", "ContractParty.Create.All", "Tạo phiên bản pháp nhân NSDLĐ mới (append-only).", "ContractParty", "All" }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 33, 1 },
                    { 34, 1 },
                    { 35, 1 },
                    { 36, 1 },
                    { 37, 1 },
                    { 38, 1 },
                    { 39, 1 },
                    { 40, 1 },
                    { 41, 1 },
                    { 42, 1 },
                    { 43, 1 },
                    { 44, 1 },
                    { 45, 1 },
                    { 46, 1 },
                    { 47, 1 },
                    { 48, 1 },
                    { 49, 1 },
                    { 50, 1 },
                    { 51, 1 },
                    { 52, 1 },
                    { 53, 1 },
                    { 54, 1 },
                    { 55, 1 },
                    { 56, 1 },
                    { 57, 1 },
                    { 58, 1 },
                    { 59, 1 },
                    { 60, 1 },
                    { 61, 1 },
                    { 62, 1 },
                    { 63, 1 },
                    { 64, 1 },
                    { 33, 2 },
                    { 35, 2 },
                    { 36, 2 },
                    { 37, 2 },
                    { 38, 2 },
                    { 39, 2 },
                    { 40, 2 },
                    { 42, 2 },
                    { 43, 2 },
                    { 44, 2 },
                    { 46, 2 },
                    { 47, 2 },
                    { 49, 2 },
                    { 51, 2 },
                    { 52, 2 },
                    { 53, 2 },
                    { 54, 2 },
                    { 55, 2 },
                    { 56, 2 },
                    { 57, 2 },
                    { 58, 2 },
                    { 59, 2 },
                    { 60, 2 },
                    { 61, 2 },
                    { 62, 2 },
                    { 33, 3 },
                    { 40, 3 },
                    { 44, 3 },
                    { 47, 3 },
                    { 34, 7 },
                    { 41, 7 },
                    { 45, 7 },
                    { 48, 7 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ArchivedAt",
                table: "Contracts",
                column: "ArchivedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contracts_ArchivedAt",
                table: "Contracts");

            migrationBuilder.DeleteData(
                table: "ContractTerminationReasonRefs",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "ContractTerminationReasonRefs",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "ContractTerminationReasonRefs",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "ContractTerminationReasonRefs",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "ContractTerminationReasonRefs",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "ContractTerminationReasonRefs",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "ContractTerminationReasonRefs",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "ContractTerminationReasonRefs",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "ContractTerminationReasonRefs",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "ContractTerminationReasonRefs",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "ContractTerminationReasonRefs",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "ContractTerminationReasonRefs",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "ContractTerminationReasonRefs",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 33, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 34, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 35, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 36, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 37, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 38, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 39, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 40, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 41, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 42, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 43, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 44, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 45, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 46, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 47, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 48, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 49, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 50, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 51, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 52, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 53, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 54, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 55, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 56, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 57, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 58, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 59, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 60, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 61, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 62, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 63, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 64, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 33, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 35, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 36, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 37, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 38, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 39, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 40, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 42, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 43, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 44, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 46, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 47, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 49, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 51, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 52, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 53, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 54, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 55, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 56, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 57, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 58, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 59, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 60, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 61, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 62, 2 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 33, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 40, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 44, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 47, 3 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 34, 7 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 41, 7 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 45, 7 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 48, 7 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 64);
        }
    }
}
