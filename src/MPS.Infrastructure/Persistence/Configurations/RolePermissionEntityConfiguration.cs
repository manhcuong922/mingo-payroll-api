using MPS.Domain.Constants;
using MPS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MPS.Infrastructure.Persistence.Configurations;

public class RolePermissionEntityConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        builder.HasKey(e => new { e.RoleId, e.PermissionId });

        builder.HasOne(e => e.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(e => e.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.PermissionId);

        builder.HasData(BuildSeed());
    }

    private static IEnumerable<RolePermission> BuildSeed()
    {
        var matrix = new (int RoleId, int[] PermissionIds)[]
        {
            (SystemRoles.SystemAdminId, AllPermissionIds()),

            (SystemRoles.HrId, new[]
            {
                PermissionIds.UserReadAll, PermissionIds.UserReadSelf, PermissionIds.UserCreateAll,
                PermissionIds.UserUpdateAll, PermissionIds.UserUpdateSelf, PermissionIds.UserDeleteAll,
                PermissionIds.EmployeeInformationReadAll, PermissionIds.EmployeeInformationReadSelf,
                PermissionIds.EmployeeInformationCreateAll, PermissionIds.EmployeeInformationUpdateAll,
                PermissionIds.EmployeeInformationUpdateSelf, PermissionIds.EmployeeInformationDeleteAll,
                PermissionIds.PositionReadAll, PermissionIds.PositionCreateAll,
                PermissionIds.PositionUpdateAll, PermissionIds.PositionDeleteAll,
                PermissionIds.DepartmentReadAll, PermissionIds.DepartmentCreateAll,
                PermissionIds.DepartmentUpdateAll, PermissionIds.DepartmentDeleteAll,
                PermissionIds.PerformanceMetricReadAll, PermissionIds.PerformanceMetricReadSelf,
                PermissionIds.TypePerformanceMetricReadAll,
                // Contract module (HRManager + HRStaff perms collapsed onto single HR role)
                PermissionIds.ContractReadAll, PermissionIds.ContractCreateAll, PermissionIds.ContractUpdateAll,
                PermissionIds.ContractDeleteAll, PermissionIds.ContractActivateAll, PermissionIds.ContractCancelAll,
                PermissionIds.ContractAppendixReadAll, PermissionIds.ContractAppendixCreateAll,
                PermissionIds.ContractAppendixActivateAll,
                PermissionIds.ContractTerminationReadAll, PermissionIds.ContractTerminationCreateAll,
                PermissionIds.ContractFileReadAll, PermissionIds.ContractFileUploadAll,
                PermissionIds.SalaryGradeReadAll, PermissionIds.SalaryGradeCreateAll,
                PermissionIds.SalaryGradeUpdateAll, PermissionIds.SalaryGradeDeleteAll,
                PermissionIds.AllowanceTypeReadAll, PermissionIds.AllowanceTypeCreateAll,
                PermissionIds.AllowanceTypeUpdateAll, PermissionIds.AllowanceTypeDeleteAll,
                PermissionIds.ContractTemplateReadAll, PermissionIds.ContractTemplateCreateAll,
                PermissionIds.ContractTemplateUpdateAll, PermissionIds.ContractTemplateDeleteAll,
                // Task/Production — HR: nghiệp vụ nhân sự (đọc đơn/task + tạo confidential)
                PermissionIds.ProjectReadAll, PermissionIds.TaskReadAll, PermissionIds.TaskCreateConfidential,
            }),

            (SystemRoles.AccountantId, new[]
            {
                PermissionIds.UserReadSelf, PermissionIds.UserUpdateSelf,
                PermissionIds.EmployeeInformationReadAll, PermissionIds.EmployeeInformationReadSelf,
                PermissionIds.EmployeeInformationUpdateSelf,
                PermissionIds.PositionReadAll,
                PermissionIds.DepartmentReadAll,
                // Contract: read-only để chạy payroll
                PermissionIds.ContractReadAll,
                PermissionIds.ContractAppendixReadAll,
                PermissionIds.ContractTerminationReadAll,
                PermissionIds.ContractFileReadAll,
                // Task/Production — Accountant theo dõi tiến độ (chỉ đọc)
                PermissionIds.ProjectReadAll, PermissionIds.TaskReadAll,
            }),

            (SystemRoles.DirectorId, new[]
            {
                PermissionIds.UserReadAll, PermissionIds.UserReadSelf, PermissionIds.UserUpdateSelf,
                PermissionIds.EmployeeInformationReadAll, PermissionIds.EmployeeInformationReadSelf,
                PermissionIds.EmployeeInformationUpdateSelf,
                PermissionIds.PositionReadAll, PermissionIds.PositionCreateAll,
                PermissionIds.PositionUpdateAll, PermissionIds.PositionDeleteAll,
                PermissionIds.DepartmentReadAll, PermissionIds.DepartmentCreateAll,
                PermissionIds.DepartmentUpdateAll, PermissionIds.DepartmentDeleteAll,
                PermissionIds.PerformanceMetricReadAll, PermissionIds.PerformanceMetricReadSelf,
                PermissionIds.PerformanceMetricApproveAll,
                PermissionIds.TypePerformanceMetricReadAll, PermissionIds.TypePerformanceMetricCreateAll,
                PermissionIds.TypePerformanceMetricUpdateAll, PermissionIds.TypePerformanceMetricDeleteAll,
                // Task/Production — GĐ xem/duyệt toàn bộ
                PermissionIds.ProcessTemplateReadAll, PermissionIds.ProcessTemplateCreateAll,
                PermissionIds.ProcessTemplateUpdateAll, PermissionIds.ProcessTemplateDeleteAll,
                PermissionIds.ProjectReadAll, PermissionIds.ProjectCreateAll, PermissionIds.ProjectUpdateAll,
                PermissionIds.ProjectDeleteAll, PermissionIds.ProjectGenerateTasks,
                PermissionIds.ProjectCompleteDepartment,
                PermissionIds.TaskReadAll, PermissionIds.TaskReadConfidential, PermissionIds.TaskCreateConfidential,
                // GĐ có quyền ghi cao nhất: chỉnh sửa task mọi xưởng, kể cả xưởng mình không trực thuộc
                // (ProjectScopeGuard.EnsureCanWriteTaskAsync bỏ qua scope khi có ProjectReadAll).
                PermissionIds.TaskCreateDepartment, PermissionIds.TaskUpdateDepartment,
                PermissionIds.TaskUpdateProgressDepartment, PermissionIds.TaskCompleteDepartment,
                PermissionIds.TaskDeleteDepartment,
                PermissionIds.ProductionAttachmentReadAll, PermissionIds.ProductionAttachmentWriteDepartment,
                PermissionIds.TaskAuditReadAll,
            }),

            // DeputyDirector = Ban kế hoạch cấp cao.
            (SystemRoles.DeputyDirectorId, new[]
            {
                PermissionIds.UserReadSelf, PermissionIds.UserUpdateSelf,
                PermissionIds.EmployeeInformationReadAll, PermissionIds.EmployeeInformationReadSelf,
                PermissionIds.EmployeeInformationUpdateSelf,
                PermissionIds.PositionReadAll,
                PermissionIds.DepartmentReadAll,
                PermissionIds.ProcessTemplateReadAll,
                PermissionIds.ProjectReadAll, PermissionIds.ProjectCreateAll, PermissionIds.ProjectGenerateTasks,
                PermissionIds.ProjectCompleteDepartment,
                PermissionIds.TaskReadAll, PermissionIds.TaskReadConfidential, PermissionIds.TaskCreateConfidential,
                PermissionIds.TaskCreateDepartment, PermissionIds.TaskUpdateDepartment,
                PermissionIds.TaskUpdateProgressDepartment, PermissionIds.TaskCompleteDepartment,
                PermissionIds.TaskDeleteDepartment,
                PermissionIds.ProductionAttachmentReadAll, PermissionIds.ProductionAttachmentWriteDepartment,
                PermissionIds.TaskAuditReadAll,
            }),

            // DepartmentManager (v4 — GỘP VAI): KHÔNG còn tham gia luồng sản xuất.
            // Không cấp quyền ProcessTemplate/Project/Task, cũng không EmployeeInformationReadAll
            // (việc xuyên xưởng + xem nhân sự đa xưởng chuyển hẳn về Director/DeputyDirector).
            // Giữ role trong seed để không phá dữ liệu cũ; chỉ còn quyền cơ bản.
            (SystemRoles.DepartmentManagerId, new[]
            {
                PermissionIds.UserReadSelf, PermissionIds.UserUpdateSelf,
                PermissionIds.EmployeeInformationReadSelf,
                PermissionIds.EmployeeInformationUpdateSelf,
                PermissionIds.PositionReadAll,
                PermissionIds.DepartmentReadAll,
                PermissionIds.PerformanceMetricReadSelf,
                PermissionIds.TypePerformanceMetricReadAll,
            }),

            // ProjectManager = tổ trưởng xưởng (Department.ManagerId) → chỉ xem nhân viên xưởng mình (Read.Department).
            (SystemRoles.ProjectManagerId, new[]
            {
                PermissionIds.UserReadSelf, PermissionIds.UserUpdateSelf,
                PermissionIds.EmployeeInformationReadDepartment, PermissionIds.EmployeeInformationReadSelf,
                PermissionIds.EmployeeInformationUpdateSelf,
                PermissionIds.PositionReadAll,
                PermissionIds.DepartmentReadAll,
                PermissionIds.PerformanceMetricReadAll, PermissionIds.PerformanceMetricReadSelf,
                PermissionIds.PerformanceMetricCreateAll, PermissionIds.PerformanceMetricUpdateAll,
                PermissionIds.PerformanceMetricDeleteAll,
                PermissionIds.TypePerformanceMetricReadAll,
                // Task/Production (v5) — quản lý xưởng: ra kế hoạch + điều phối, scoped theo xưởng trực thuộc.
                PermissionIds.ProcessTemplateReadAll,
                PermissionIds.ProjectReadDepartment, PermissionIds.ProjectCreateAll,
                PermissionIds.ProjectUpdateAll, PermissionIds.ProjectGenerateTasks,
                PermissionIds.ProjectCompleteDepartment,
                PermissionIds.TaskReadDepartment, PermissionIds.TaskCreateDepartment,
                PermissionIds.TaskUpdateDepartment, PermissionIds.TaskUpdateProgressDepartment,
                PermissionIds.TaskCompleteDepartment, PermissionIds.TaskDeleteDepartment,
                PermissionIds.ProductionAttachmentReadDepartment, PermissionIds.ProductionAttachmentWriteDepartment,
                PermissionIds.TaskAuditReadDepartment,
            }),

            (SystemRoles.EmployeeId, new[]
            {
                PermissionIds.UserReadSelf, PermissionIds.UserUpdateSelf,
                PermissionIds.EmployeeInformationReadSelf, PermissionIds.EmployeeInformationUpdateSelf,
                PermissionIds.PositionReadAll,
                PermissionIds.DepartmentReadAll,
                PermissionIds.PerformanceMetricReadSelf,
                PermissionIds.TypePerformanceMetricReadAll,
                // Contract: chỉ xem HĐ của chính mình (self-scope)
                PermissionIds.ContractReadSelf,
                PermissionIds.ContractAppendixReadSelf,
                PermissionIds.ContractTerminationReadSelf,
                PermissionIds.ContractFileReadSelf,
                // Task/Production — công nhân chỉ XEM task của mình + tài liệu xưởng.
                PermissionIds.TaskReadAssigned,
                PermissionIds.ProductionAttachmentReadDepartment,
            }),
        };

        return matrix.SelectMany(row => row.PermissionIds
            .Select(pid => new RolePermission { RoleId = row.RoleId, PermissionId = pid }));
    }

    private static int[] AllPermissionIds() => new[]
    {
        PermissionIds.UserReadAll, PermissionIds.UserReadSelf, PermissionIds.UserCreateAll,
        PermissionIds.UserUpdateAll, PermissionIds.UserUpdateSelf, PermissionIds.UserDeleteAll,
        PermissionIds.EmployeeInformationReadAll, PermissionIds.EmployeeInformationReadSelf,
        PermissionIds.EmployeeInformationCreateAll, PermissionIds.EmployeeInformationUpdateAll,
        PermissionIds.EmployeeInformationUpdateSelf, PermissionIds.EmployeeInformationDeleteAll,
        PermissionIds.PositionReadAll, PermissionIds.PositionCreateAll,
        PermissionIds.PositionUpdateAll, PermissionIds.PositionDeleteAll,
        PermissionIds.PerformanceMetricReadAll, PermissionIds.PerformanceMetricReadSelf,
        PermissionIds.PerformanceMetricCreateAll, PermissionIds.PerformanceMetricUpdateAll,
        PermissionIds.PerformanceMetricDeleteAll, PermissionIds.PerformanceMetricApproveAll,
        PermissionIds.TypePerformanceMetricReadAll, PermissionIds.TypePerformanceMetricCreateAll,
        PermissionIds.TypePerformanceMetricUpdateAll, PermissionIds.TypePerformanceMetricDeleteAll,
        PermissionIds.RoleReadAll, PermissionIds.RoleCreateAll, PermissionIds.RoleUpdateAll,
        PermissionIds.RoleDeleteAll, PermissionIds.RoleManagePermissionsAll,
        PermissionIds.RefreshTokenRevokeAll,
        // Contract module
        PermissionIds.ContractReadAll, PermissionIds.ContractReadSelf, PermissionIds.ContractCreateAll,
        PermissionIds.ContractUpdateAll, PermissionIds.ContractDeleteAll, PermissionIds.ContractActivateAll,
        PermissionIds.ContractCancelAll,
        PermissionIds.ContractAppendixReadAll, PermissionIds.ContractAppendixReadSelf,
        PermissionIds.ContractAppendixCreateAll, PermissionIds.ContractAppendixActivateAll,
        PermissionIds.ContractTerminationReadAll, PermissionIds.ContractTerminationReadSelf,
        PermissionIds.ContractTerminationCreateAll,
        PermissionIds.ContractFileReadAll, PermissionIds.ContractFileReadSelf,
        PermissionIds.ContractFileUploadAll, PermissionIds.ContractFileDeleteAll,
        PermissionIds.SalaryGradeReadAll, PermissionIds.SalaryGradeCreateAll,
        PermissionIds.SalaryGradeUpdateAll, PermissionIds.SalaryGradeDeleteAll,
        PermissionIds.AllowanceTypeReadAll, PermissionIds.AllowanceTypeCreateAll,
        PermissionIds.AllowanceTypeUpdateAll, PermissionIds.AllowanceTypeDeleteAll,
        PermissionIds.ContractTemplateReadAll, PermissionIds.ContractTemplateCreateAll,
        PermissionIds.ContractTemplateUpdateAll, PermissionIds.ContractTemplateDeleteAll,
        PermissionIds.ContractPartyReadAll, PermissionIds.ContractPartyCreateAll,
        // Department module
        PermissionIds.DepartmentReadAll, PermissionIds.DepartmentCreateAll,
        PermissionIds.DepartmentUpdateAll, PermissionIds.DepartmentDeleteAll,
        PermissionIds.EmployeeInformationReadDepartment,
        // Task/Production module
        PermissionIds.ProcessTemplateReadAll, PermissionIds.ProcessTemplateCreateAll,
        PermissionIds.ProcessTemplateUpdateAll, PermissionIds.ProcessTemplateDeleteAll,
        PermissionIds.ProjectReadAll, PermissionIds.ProjectReadDepartment, PermissionIds.ProjectCreateAll,
        PermissionIds.ProjectUpdateAll, PermissionIds.ProjectDeleteAll, PermissionIds.ProjectGenerateTasks,
        PermissionIds.ProjectCompleteDepartment,
        PermissionIds.TaskReadAll, PermissionIds.TaskReadDepartment, PermissionIds.TaskReadAssigned,
        PermissionIds.TaskReadConfidential,
        PermissionIds.TaskCreateDepartment, PermissionIds.TaskCreateConfidential,
        PermissionIds.TaskUpdateDepartment, PermissionIds.TaskUpdateProgressDepartment,
        PermissionIds.TaskCompleteDepartment, PermissionIds.TaskDeleteDepartment,
        PermissionIds.ProductionAttachmentReadDepartment, PermissionIds.ProductionAttachmentWriteDepartment,
        PermissionIds.ProductionAttachmentReadAll,
        PermissionIds.TaskAuditReadAll, PermissionIds.TaskAuditReadDepartment,
    };
}
