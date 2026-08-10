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
                // Evaluation — HR: xem tất cả, tổng hợp tháng, duyệt/khóa/reopen + tự xem điểm mình
                PermissionIds.OrderEvaluationReadAll,
                PermissionIds.MonthlyEvaluationReadAll, PermissionIds.MonthlyEvaluationReadSelf,
                PermissionIds.MonthlyEvaluationAggregateAll, PermissionIds.MonthlyEvaluationApproveAll,
                PermissionIds.MonthlyEvaluationReopenAll,
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
                // Evaluation — Kế toán tự xem điểm mình
                PermissionIds.MonthlyEvaluationReadSelf,
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
                PermissionIds.ProductionAttachmentReadAll,
                PermissionIds.ProductionAttachmentReadProject, PermissionIds.ProductionAttachmentWriteProject,
                PermissionIds.ProductionAttachmentReadStage, PermissionIds.ProductionAttachmentWriteStage,
                PermissionIds.TaskAuditReadAll,
                // Evaluation — Director: xem tất cả + chấm trực tiếp tổ trưởng/văn phòng + tự xem điểm mình
                PermissionIds.OrderEvaluationReadAll,
                PermissionIds.MonthlyEvaluationReadAll, PermissionIds.MonthlyEvaluationReadSelf,
                PermissionIds.MonthlyEvaluationScoreDirectAll,
            }),

            // DepartmentManager = quản lý 1 xưởng cụ thể (trực thuộc qua EmployeeInformation.DepartmentId,
            // xem SystemRoles.DepartmentMemberRoleIds). Xem TOÀN BỘ 3 tầng (đơn/công đoạn/task) của xưởng
            // mình, không bị giới hạn theo AssignedPmUserId như ProjectManager — dùng
            // Project.Read.FullDepartment/Task.Read.FullDepartment (permission-based, không hardcode role,
            // xem ProjectScopeGuard.CanSeeFullDepartmentProjects/Tasks). Không có quyền ghi (chỉ quan sát).
            (SystemRoles.DepartmentManagerId, new[]
            {
                PermissionIds.UserReadSelf, PermissionIds.UserUpdateSelf,
                PermissionIds.EmployeeInformationReadSelf,
                PermissionIds.EmployeeInformationUpdateSelf,
                PermissionIds.PositionReadAll,
                PermissionIds.DepartmentReadAll,
                PermissionIds.PerformanceMetricReadSelf,
                PermissionIds.TypePerformanceMetricReadAll,
                PermissionIds.ProjectReadFullDepartment,
                PermissionIds.TaskReadFullDepartment,
                // Tài liệu/ảnh: xem + đăng tải cả tầng 1 (Đơn hàng) và tầng 2+3 (Công đoạn/Task lá) của
                // xưởng mình quản lý.
                PermissionIds.ProductionAttachmentReadProject, PermissionIds.ProductionAttachmentWriteProject,
                PermissionIds.ProductionAttachmentReadStage, PermissionIds.ProductionAttachmentWriteStage,
                // Evaluation — Planning Manager tự xem điểm mình
                PermissionIds.MonthlyEvaluationReadSelf,
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
                // Task/Production (v5) — quản lý xưởng: điều phối, scoped theo xưởng trực thuộc. PM KHÔNG
                // chạm vào cấp Đơn hàng ở bất kỳ thao tác nào (không tự tạo đơn, không áp quy trình sinh
                // task, không sửa/hoàn thành/đóng đơn) — chỉ xem đơn (ReadDepartment, đã bị FilterByAssignedPm
                // giới hạn còn công đoạn của mình) và thao tác ở cấp Công đoạn/Task. Việc sinh task ban đầu
                // và quản lý đơn là của GĐ/Phó GĐ.
                PermissionIds.ProjectReadDepartment,
                PermissionIds.TaskReadDepartment, PermissionIds.TaskCreateDepartment,
                PermissionIds.TaskUpdateDepartment, PermissionIds.TaskUpdateProgressDepartment,
                PermissionIds.TaskCompleteDepartment, PermissionIds.TaskDeleteDepartment,
                // Tài liệu/ảnh: chỉ tầng 2+3 (Công đoạn/Task lá) — PM KHÔNG xem/đăng tài liệu cấp Đơn hàng.
                PermissionIds.ProductionAttachmentReadStage, PermissionIds.ProductionAttachmentWriteStage,
                PermissionIds.TaskAuditReadDepartment,
                // Evaluation — Tổ trưởng: chấm/submit bảng-theo-đơn mình phụ trách + tự xem điểm mình
                PermissionIds.OrderEvaluationReadOwn, PermissionIds.OrderEvaluationScoreOwn,
                PermissionIds.OrderEvaluationSubmitOwn, PermissionIds.MonthlyEvaluationReadSelf,
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
                // Task/Production — công nhân chỉ XEM task của mình + tài liệu tầng 2+3 (không đăng tải,
                // không xem tài liệu cấp Đơn hàng).
                PermissionIds.TaskReadAssigned,
                PermissionIds.ProductionAttachmentReadStage,
                // Evaluation — Công nhân tự xem điểm tháng của mình
                PermissionIds.MonthlyEvaluationReadSelf,
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
        PermissionIds.ProductionAttachmentReadProject, PermissionIds.ProductionAttachmentWriteProject,
        PermissionIds.ProductionAttachmentReadStage, PermissionIds.ProductionAttachmentWriteStage,
        PermissionIds.TaskAuditReadAll, PermissionIds.TaskAuditReadDepartment,
        // Evaluation module
        PermissionIds.OrderEvaluationReadOwn, PermissionIds.OrderEvaluationReadAll,
        PermissionIds.OrderEvaluationScoreOwn, PermissionIds.OrderEvaluationSubmitOwn,
        PermissionIds.MonthlyEvaluationReadAll, PermissionIds.MonthlyEvaluationReadSelf,
        PermissionIds.MonthlyEvaluationAggregateAll, PermissionIds.MonthlyEvaluationScoreDirectAll,
        PermissionIds.MonthlyEvaluationApproveAll, PermissionIds.MonthlyEvaluationReopenAll,
        // ===== Compliance module — lỗi #31 =====
        // Trước đây nhóm này KHÔNG có mặt ở đây, nên ma trận seed cấp cho SystemAdmin thiếu sạch
        // quyền đọc dữ liệu tuân thủ. Việc cấp quyền phụ thuộc HOÀN TOÀN vào migration
        // 20260612100000_ComplianceRolePermissions — mà migration đó lại thiếu attribute
        // [Migration] nên chưa bao giờ được EF nhận vào chuỗi ⇒ mọi môi trường dựng từ code
        // (CI, staging, máy mới, production) đều 403 ở toàn bộ GET /compliance/*, kể cả admin.
        // Nguồn sự thật phải nằm ở ĐÂY; migration chỉ là đường vá cho DB đã tồn tại.
        PermissionIds.SocialInsuranceRateReadAll, PermissionIds.SocialInsuranceRateCreateAll,
        PermissionIds.SocialInsuranceRateUpdateAll, PermissionIds.SocialInsuranceRateDeleteAll,
        PermissionIds.PitRegulationReadAll, PermissionIds.PitRegulationCreateAll,
        PermissionIds.PitRegulationUpdateAll, PermissionIds.PitRegulationDeleteAll,
        PermissionIds.MinimumWageReadAll, PermissionIds.MinimumWageCreateAll,
        PermissionIds.MinimumWageUpdateAll, PermissionIds.MinimumWageDeleteAll,
        PermissionIds.AllowanceExemptThresholdReadAll, PermissionIds.AllowanceExemptThresholdCreateAll,
        PermissionIds.AllowanceExemptThresholdUpdateAll, PermissionIds.AllowanceExemptThresholdDeleteAll,
        PermissionIds.LaborLawConstantsReadAll, PermissionIds.LaborLawConstantsCreateAll,
        PermissionIds.LaborLawConstantsUpdateAll, PermissionIds.LaborLawConstantsDeleteAll,
        PermissionIds.LegalDocumentReadAll, PermissionIds.LegalDocumentWriteAll,
        PermissionIds.AuditLogReadAll,
    };
}
