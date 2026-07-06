using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MPS.Infrastructure.Persistence.Configurations;

public class PermissionEntityConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.Code).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Resource).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Scope).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(255).IsRequired();

        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => new { e.Resource, e.Action, e.Scope });

        builder.HasData(BuildMasterPermissions());
    }

    private static IEnumerable<Permission> BuildMasterPermissions() => new[]
    {
        P(PermissionIds.UserReadAll, Permissions.UserReadAll, "User", "Read", PermissionScope.All, "Xem tất cả người dùng."),
        P(PermissionIds.UserReadSelf, Permissions.UserReadSelf, "User", "Read", PermissionScope.Self, "Xem thông tin tài khoản của chính mình."),
        P(PermissionIds.UserCreateAll, Permissions.UserCreateAll, "User", "Create", PermissionScope.All, "Tạo người dùng mới."),
        P(PermissionIds.UserUpdateAll, Permissions.UserUpdateAll, "User", "Update", PermissionScope.All, "Cập nhật bất kỳ người dùng nào."),
        P(PermissionIds.UserUpdateSelf, Permissions.UserUpdateSelf, "User", "Update", PermissionScope.Self, "Cập nhật thông tin của chính mình."),
        P(PermissionIds.UserDeleteAll, Permissions.UserDeleteAll, "User", "Delete", PermissionScope.All, "Xóa người dùng."),

        P(PermissionIds.EmployeeInformationReadAll, Permissions.EmployeeInformationReadAll, "EmployeeInformation", "Read", PermissionScope.All, "Xem hồ sơ nhân sự của tất cả nhân viên."),
        P(PermissionIds.EmployeeInformationReadSelf, Permissions.EmployeeInformationReadSelf, "EmployeeInformation", "Read", PermissionScope.Self, "Xem hồ sơ nhân sự của chính mình."),
        P(PermissionIds.EmployeeInformationCreateAll, Permissions.EmployeeInformationCreateAll, "EmployeeInformation", "Create", PermissionScope.All, "Tạo hồ sơ nhân sự."),
        P(PermissionIds.EmployeeInformationUpdateAll, Permissions.EmployeeInformationUpdateAll, "EmployeeInformation", "Update", PermissionScope.All, "Cập nhật hồ sơ nhân sự của bất kỳ ai."),
        P(PermissionIds.EmployeeInformationUpdateSelf, Permissions.EmployeeInformationUpdateSelf, "EmployeeInformation", "Update", PermissionScope.Self, "Cập nhật hồ sơ nhân sự của chính mình."),
        P(PermissionIds.EmployeeInformationDeleteAll, Permissions.EmployeeInformationDeleteAll, "EmployeeInformation", "Delete", PermissionScope.All, "Xóa hồ sơ nhân sự."),

        P(PermissionIds.PositionReadAll, Permissions.PositionReadAll, "Position", "Read", PermissionScope.All, "Xem danh sách chức vụ."),
        P(PermissionIds.PositionCreateAll, Permissions.PositionCreateAll, "Position", "Create", PermissionScope.All, "Tạo chức vụ."),
        P(PermissionIds.PositionUpdateAll, Permissions.PositionUpdateAll, "Position", "Update", PermissionScope.All, "Cập nhật chức vụ."),
        P(PermissionIds.PositionDeleteAll, Permissions.PositionDeleteAll, "Position", "Delete", PermissionScope.All, "Xóa chức vụ."),

        P(PermissionIds.PerformanceMetricReadAll, Permissions.PerformanceMetricReadAll, "PerformanceMetric", "Read", PermissionScope.All, "Xem chỉ tiêu KPI của tất cả nhân viên."),
        P(PermissionIds.PerformanceMetricReadSelf, Permissions.PerformanceMetricReadSelf, "PerformanceMetric", "Read", PermissionScope.Self, "Xem KPI của chính mình."),
        P(PermissionIds.PerformanceMetricCreateAll, Permissions.PerformanceMetricCreateAll, "PerformanceMetric", "Create", PermissionScope.All, "Tạo chỉ tiêu KPI."),
        P(PermissionIds.PerformanceMetricUpdateAll, Permissions.PerformanceMetricUpdateAll, "PerformanceMetric", "Update", PermissionScope.All, "Cập nhật chỉ tiêu KPI."),
        P(PermissionIds.PerformanceMetricDeleteAll, Permissions.PerformanceMetricDeleteAll, "PerformanceMetric", "Delete", PermissionScope.All, "Xóa chỉ tiêu KPI."),
        P(PermissionIds.PerformanceMetricApproveAll, Permissions.PerformanceMetricApproveAll, "PerformanceMetric", "Approve", PermissionScope.All, "Duyệt KPI."),

        P(PermissionIds.TypePerformanceMetricReadAll, Permissions.TypePerformanceMetricReadAll, "TypePerformanceMetric", "Read", PermissionScope.All, "Xem loại KPI."),
        P(PermissionIds.TypePerformanceMetricCreateAll, Permissions.TypePerformanceMetricCreateAll, "TypePerformanceMetric", "Create", PermissionScope.All, "Tạo loại KPI."),
        P(PermissionIds.TypePerformanceMetricUpdateAll, Permissions.TypePerformanceMetricUpdateAll, "TypePerformanceMetric", "Update", PermissionScope.All, "Cập nhật loại KPI."),
        P(PermissionIds.TypePerformanceMetricDeleteAll, Permissions.TypePerformanceMetricDeleteAll, "TypePerformanceMetric", "Delete", PermissionScope.All, "Xóa loại KPI."),

        P(PermissionIds.RoleReadAll, Permissions.RoleReadAll, "Role", "Read", PermissionScope.All, "Xem role và permission."),
        P(PermissionIds.RoleCreateAll, Permissions.RoleCreateAll, "Role", "Create", PermissionScope.All, "Tạo role mới."),
        P(PermissionIds.RoleUpdateAll, Permissions.RoleUpdateAll, "Role", "Update", PermissionScope.All, "Cập nhật role (chặn system role)."),
        P(PermissionIds.RoleDeleteAll, Permissions.RoleDeleteAll, "Role", "Delete", PermissionScope.All, "Xóa role (chặn system role hoặc role còn user)."),
        P(PermissionIds.RoleManagePermissionsAll, Permissions.RoleManagePermissionsAll, "Role", "ManagePermissions", PermissionScope.All, "Gán/sửa permission của role."),

        P(PermissionIds.RefreshTokenRevokeAll, Permissions.RefreshTokenRevokeAll, "RefreshToken", "Revoke", PermissionScope.All, "Force logout user khác."),

        // ===== Contract module (Phase 2) — IDs 33–64 =====

        P(PermissionIds.ContractReadAll, Permissions.ContractReadAll, "Contract", "Read", PermissionScope.All, "Xem tất cả HĐLĐ."),
        P(PermissionIds.ContractReadSelf, Permissions.ContractReadSelf, "Contract", "Read", PermissionScope.Self, "Xem HĐLĐ của chính mình."),
        P(PermissionIds.ContractCreateAll, Permissions.ContractCreateAll, "Contract", "Create", PermissionScope.All, "Tạo HĐLĐ mới (DRAFT)."),
        P(PermissionIds.ContractUpdateAll, Permissions.ContractUpdateAll, "Contract", "Update", PermissionScope.All, "Cập nhật HĐLĐ ở trạng thái DRAFT."),
        P(PermissionIds.ContractDeleteAll, Permissions.ContractDeleteAll, "Contract", "Delete", PermissionScope.All, "Xóa HĐLĐ DRAFT."),
        P(PermissionIds.ContractActivateAll, Permissions.ContractActivateAll, "Contract", "Activate", PermissionScope.All, "Upload PDF đã ký và kích hoạt HĐLĐ (DRAFT → ACTIVE)."),
        P(PermissionIds.ContractCancelAll, Permissions.ContractCancelAll, "Contract", "Cancel", PermissionScope.All, "Hủy HĐLĐ DRAFT (DRAFT → CANCELLED)."),

        P(PermissionIds.ContractAppendixReadAll, Permissions.ContractAppendixReadAll, "ContractAppendix", "Read", PermissionScope.All, "Xem phụ lục HĐLĐ của bất kỳ HĐ nào."),
        P(PermissionIds.ContractAppendixReadSelf, Permissions.ContractAppendixReadSelf, "ContractAppendix", "Read", PermissionScope.Self, "Xem phụ lục HĐLĐ của chính mình."),
        P(PermissionIds.ContractAppendixCreateAll, Permissions.ContractAppendixCreateAll, "ContractAppendix", "Create", PermissionScope.All, "Tạo phụ lục HĐLĐ (DRAFT)."),
        P(PermissionIds.ContractAppendixActivateAll, Permissions.ContractAppendixActivateAll, "ContractAppendix", "Activate", PermissionScope.All, "Upload PDF phụ lục đã ký và áp dụng thay đổi."),

        P(PermissionIds.ContractTerminationReadAll, Permissions.ContractTerminationReadAll, "ContractTermination", "Read", PermissionScope.All, "Xem hồ sơ chấm dứt HĐLĐ của bất kỳ HĐ nào."),
        P(PermissionIds.ContractTerminationReadSelf, Permissions.ContractTerminationReadSelf, "ContractTermination", "Read", PermissionScope.Self, "Xem hồ sơ chấm dứt HĐLĐ của chính mình."),
        P(PermissionIds.ContractTerminationCreateAll, Permissions.ContractTerminationCreateAll, "ContractTermination", "Create", PermissionScope.All, "Tạo hồ sơ chấm dứt HĐLĐ."),

        P(PermissionIds.ContractFileReadAll, Permissions.ContractFileReadAll, "ContractFile", "Read", PermissionScope.All, "Tải file HĐ/phụ lục/thông báo của bất kỳ HĐ nào."),
        P(PermissionIds.ContractFileReadSelf, Permissions.ContractFileReadSelf, "ContractFile", "Read", PermissionScope.Self, "Tải file HĐ của chính mình."),
        P(PermissionIds.ContractFileUploadAll, Permissions.ContractFileUploadAll, "ContractFile", "Upload", PermissionScope.All, "Upload file PDF đã ký lên MinIO."),
        P(PermissionIds.ContractFileDeleteAll, Permissions.ContractFileDeleteAll, "ContractFile", "Delete", PermissionScope.All, "Xóa file HĐ trên MinIO (chỉ SystemAdmin)."),

        P(PermissionIds.SalaryGradeReadAll, Permissions.SalaryGradeReadAll, "SalaryGrade", "Read", PermissionScope.All, "Xem catalog bậc lương."),
        P(PermissionIds.SalaryGradeCreateAll, Permissions.SalaryGradeCreateAll, "SalaryGrade", "Create", PermissionScope.All, "Tạo bậc lương."),
        P(PermissionIds.SalaryGradeUpdateAll, Permissions.SalaryGradeUpdateAll, "SalaryGrade", "Update", PermissionScope.All, "Cập nhật bậc lương."),
        P(PermissionIds.SalaryGradeDeleteAll, Permissions.SalaryGradeDeleteAll, "SalaryGrade", "Delete", PermissionScope.All, "Xóa bậc lương (soft nếu đã được HĐ tham chiếu)."),

        P(PermissionIds.AllowanceTypeReadAll, Permissions.AllowanceTypeReadAll, "AllowanceType", "Read", PermissionScope.All, "Xem catalog phụ cấp."),
        P(PermissionIds.AllowanceTypeCreateAll, Permissions.AllowanceTypeCreateAll, "AllowanceType", "Create", PermissionScope.All, "Tạo loại phụ cấp."),
        P(PermissionIds.AllowanceTypeUpdateAll, Permissions.AllowanceTypeUpdateAll, "AllowanceType", "Update", PermissionScope.All, "Cập nhật loại phụ cấp."),
        P(PermissionIds.AllowanceTypeDeleteAll, Permissions.AllowanceTypeDeleteAll, "AllowanceType", "Delete", PermissionScope.All, "Xóa loại phụ cấp (soft nếu đã được HĐ tham chiếu)."),

        P(PermissionIds.ContractTemplateReadAll, Permissions.ContractTemplateReadAll, "ContractTemplate", "Read", PermissionScope.All, "Xem mẫu HĐLĐ."),
        P(PermissionIds.ContractTemplateCreateAll, Permissions.ContractTemplateCreateAll, "ContractTemplate", "Create", PermissionScope.All, "Tạo mẫu HĐLĐ."),
        P(PermissionIds.ContractTemplateUpdateAll, Permissions.ContractTemplateUpdateAll, "ContractTemplate", "Update", PermissionScope.All, "Cập nhật mẫu HĐLĐ (bump Version)."),
        P(PermissionIds.ContractTemplateDeleteAll, Permissions.ContractTemplateDeleteAll, "ContractTemplate", "Delete", PermissionScope.All, "Xóa mẫu HĐLĐ (soft)."),

        P(PermissionIds.ContractPartyReadAll, Permissions.ContractPartyReadAll, "ContractParty", "Read", PermissionScope.All, "Xem pháp nhân NSDLĐ."),
        P(PermissionIds.ContractPartyCreateAll, Permissions.ContractPartyCreateAll, "ContractParty", "Create", PermissionScope.All, "Tạo phiên bản pháp nhân NSDLĐ mới (append-only)."),

        // ===== Department module - IDs 143-147 =====
        // LegalDocument (89/91), AuditLog (90), ComplianceAutoUpdate (92-94) được seed
        // qua migrations riêng — KHÔNG đặt ở đây để tránh EF sinh INSERT duplicate.

        P(PermissionIds.DepartmentReadAll,   Permissions.DepartmentReadAll,   "Department", "Read",   PermissionScope.All, "Xem danh sách phòng ban."),
        P(PermissionIds.DepartmentCreateAll, Permissions.DepartmentCreateAll, "Department", "Create", PermissionScope.All, "Tạo phòng ban."),
        P(PermissionIds.DepartmentUpdateAll, Permissions.DepartmentUpdateAll, "Department", "Update", PermissionScope.All, "Cập nhật phòng ban."),
        P(PermissionIds.DepartmentDeleteAll, Permissions.DepartmentDeleteAll, "Department", "Delete", PermissionScope.All, "Xóa phòng ban."),

        P(PermissionIds.EmployeeInformationReadDepartment, Permissions.EmployeeInformationReadDepartment,
            "EmployeeInformation", "Read", "Department", "Xem nhân viên trong phòng ban mình quản lý."),

        // ===== Task/Production module - IDs 148-173 =====
        P(PermissionIds.ProcessTemplateReadAll,   Permissions.ProcessTemplateReadAll,   "ProcessTemplate", "Read",   PermissionScope.All, "Xem quy trình mẫu."),
        P(PermissionIds.ProcessTemplateCreateAll, Permissions.ProcessTemplateCreateAll, "ProcessTemplate", "Create", PermissionScope.All, "Tạo quy trình mẫu."),
        P(PermissionIds.ProcessTemplateUpdateAll, Permissions.ProcessTemplateUpdateAll, "ProcessTemplate", "Update", PermissionScope.All, "Cập nhật quy trình mẫu."),
        P(PermissionIds.ProcessTemplateDeleteAll, Permissions.ProcessTemplateDeleteAll, "ProcessTemplate", "Delete", PermissionScope.All, "Xóa quy trình mẫu."),

        P(PermissionIds.ProjectReadAll,        Permissions.ProjectReadAll,        "Project", "Read",          PermissionScope.All,        "Xem mọi đơn hàng."),
        P(PermissionIds.ProjectReadDepartment, Permissions.ProjectReadDepartment, "Project", "Read",          "Department",               "Xem đơn hàng của xưởng mình."),
        P(PermissionIds.ProjectCreateAll,      Permissions.ProjectCreateAll,      "Project", "Create",        PermissionScope.All,        "Tạo đơn hàng."),
        P(PermissionIds.ProjectUpdateAll,      Permissions.ProjectUpdateAll,      "Project", "Update",        PermissionScope.All,        "Cập nhật đơn hàng."),
        P(PermissionIds.ProjectDeleteAll,      Permissions.ProjectDeleteAll,      "Project", "Delete",        PermissionScope.All,        "Xóa đơn hàng."),
        P(PermissionIds.ProjectGenerateTasks,  Permissions.ProjectGenerateTasks,  "Project", "GenerateTasks", PermissionScope.All,        "Sinh task từ quy trình mẫu."),
        P(PermissionIds.ProjectCompleteDepartment, Permissions.ProjectCompleteDepartment, "Project", "Complete", "Department",       "Đóng đơn của xưởng mình."),

        P(PermissionIds.TaskReadAll,          Permissions.TaskReadAll,          "Task", "Read", PermissionScope.All, "Xem mọi task."),
        P(PermissionIds.TaskReadDepartment,   Permissions.TaskReadDepartment,   "Task", "Read", "Department",        "Xem task của xưởng mình."),
        P(PermissionIds.TaskReadAssigned,     Permissions.TaskReadAssigned,     "Task", "Read", "Assigned",          "Xem task được giao cho mình."),
        P(PermissionIds.TaskReadConfidential, Permissions.TaskReadConfidential, "Task", "Read", "Confidential",      "Xem task nhạy cảm."),

        P(PermissionIds.TaskCreateDepartment,         Permissions.TaskCreateDepartment,         "Task", "Create",         "Department",   "Tạo task (1 người / nhóm) trong xưởng mình."),
        P(PermissionIds.TaskCreateConfidential,       Permissions.TaskCreateConfidential,       "Task", "Create",         "Confidential", "Tạo task nhạy cảm."),
        P(PermissionIds.TaskUpdateDepartment,         Permissions.TaskUpdateDepartment,         "Task", "Update",         "Department",   "Sửa task của xưởng mình."),
        P(PermissionIds.TaskUpdateProgressDepartment, Permissions.TaskUpdateProgressDepartment, "Task", "UpdateProgress", "Department",   "Cập nhật sản lượng task xưởng mình."),
        P(PermissionIds.TaskCompleteDepartment,       Permissions.TaskCompleteDepartment,       "Task", "Complete",       "Department",   "Hoàn thành task xưởng mình."),
        P(PermissionIds.TaskDeleteDepartment,         Permissions.TaskDeleteDepartment,         "Task", "Delete",         "Department",   "Xóa task lá của xưởng mình."),

        P(PermissionIds.ProductionAttachmentReadDepartment,  Permissions.ProductionAttachmentReadDepartment,  "ProductionAttachment", "Read",  "Department", "Xem tài liệu/ảnh của xưởng mình."),
        P(PermissionIds.ProductionAttachmentWriteDepartment, Permissions.ProductionAttachmentWriteDepartment, "ProductionAttachment", "Write", "Department", "Tải lên/xóa tài liệu của xưởng mình."),
        P(PermissionIds.ProductionAttachmentReadAll,         Permissions.ProductionAttachmentReadAll,         "ProductionAttachment", "Read",  PermissionScope.All, "Xem tài liệu/ảnh mọi xưởng."),

        P(PermissionIds.TaskAuditReadAll,        Permissions.TaskAuditReadAll,        "Task", "Audit.Read", PermissionScope.All, "Xem lịch sử chỉnh sửa task/đơn mọi xưởng."),
        P(PermissionIds.TaskAuditReadDepartment, Permissions.TaskAuditReadDepartment, "Task", "Audit.Read", "Department",        "Xem lịch sử chỉnh sửa task/đơn xưởng mình."),
    };

    private static Permission P(int id, string code, string resource, string action, string scope, string description) => new()
    {
        Id = id,
        Code = code,
        Resource = resource,
        Action = action,
        Scope = scope,
        Description = description,
    };
}
