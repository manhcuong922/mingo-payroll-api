namespace MPS.Domain.Constants;

// Chú ý: ID trong PermissionIds đã được seed vào DB — KHÔNG đổi giá trị ID cũ.
// Khi thêm permission mới, dùng ID tiếp theo trong dải trống (xem comment "reserved").

public static class Permissions
{
    // ===== Core: User + EmployeeInformation + Position + Metric + Role + RefreshToken — IDs 1–32 =====

    public const string UserReadAll = "User.Read.All";
    public const string UserReadSelf = "User.Read.Self";
    public const string UserCreateAll = "User.Create.All";
    public const string UserUpdateAll = "User.Update.All";
    public const string UserUpdateSelf = "User.Update.Self";
    public const string UserDeleteAll = "User.Delete.All";

    public const string EmployeeInformationReadAll = "EmployeeInformation.Read.All";
    public const string EmployeeInformationReadSelf = "EmployeeInformation.Read.Self";
    public const string EmployeeInformationCreateAll = "EmployeeInformation.Create.All";
    public const string EmployeeInformationUpdateAll = "EmployeeInformation.Update.All";
    public const string EmployeeInformationUpdateSelf = "EmployeeInformation.Update.Self";
    public const string EmployeeInformationDeleteAll = "EmployeeInformation.Delete.All";

    public const string PositionReadAll = "Position.Read.All";
    public const string PositionCreateAll = "Position.Create.All";
    public const string PositionUpdateAll = "Position.Update.All";
    public const string PositionDeleteAll = "Position.Delete.All";

    public const string PerformanceMetricReadAll = "PerformanceMetric.Read.All";
    public const string PerformanceMetricReadSelf = "PerformanceMetric.Read.Self";
    public const string PerformanceMetricCreateAll = "PerformanceMetric.Create.All";
    public const string PerformanceMetricUpdateAll = "PerformanceMetric.Update.All";
    public const string PerformanceMetricDeleteAll = "PerformanceMetric.Delete.All";
    public const string PerformanceMetricApproveAll = "PerformanceMetric.Approve.All";

    public const string TypePerformanceMetricReadAll = "TypePerformanceMetric.Read.All";
    public const string TypePerformanceMetricCreateAll = "TypePerformanceMetric.Create.All";
    public const string TypePerformanceMetricUpdateAll = "TypePerformanceMetric.Update.All";
    public const string TypePerformanceMetricDeleteAll = "TypePerformanceMetric.Delete.All";

    public const string RoleReadAll = "Role.Read.All";
    public const string RoleCreateAll = "Role.Create.All";
    public const string RoleUpdateAll = "Role.Update.All";
    public const string RoleDeleteAll = "Role.Delete.All";
    public const string RoleManagePermissionsAll = "Role.ManagePermissions.All";

    public const string RefreshTokenRevokeAll = "RefreshToken.Revoke.All";

    // ===== Contract module (Phase 1) — IDs 33–64 =====

    public const string ContractReadAll = "Contract.Read.All";
    public const string ContractReadSelf = "Contract.Read.Self";
    public const string ContractCreateAll = "Contract.Create.All";
    public const string ContractUpdateAll = "Contract.Update.All";
    public const string ContractDeleteAll = "Contract.Delete.All";
    public const string ContractActivateAll = "Contract.Activate.All";
    public const string ContractCancelAll = "Contract.Cancel.All";

    public const string ContractAppendixReadAll = "ContractAppendix.Read.All";
    public const string ContractAppendixReadSelf = "ContractAppendix.Read.Self";
    public const string ContractAppendixCreateAll = "ContractAppendix.Create.All";
    public const string ContractAppendixActivateAll = "ContractAppendix.Activate.All";

    public const string ContractTerminationReadAll = "ContractTermination.Read.All";
    public const string ContractTerminationReadSelf = "ContractTermination.Read.Self";
    public const string ContractTerminationCreateAll = "ContractTermination.Create.All";

    public const string ContractFileReadAll = "ContractFile.Read.All";
    public const string ContractFileReadSelf = "ContractFile.Read.Self";
    public const string ContractFileUploadAll = "ContractFile.Upload.All";
    public const string ContractFileDeleteAll = "ContractFile.Delete.All";

    public const string SalaryGradeReadAll = "SalaryGrade.Read.All";
    public const string SalaryGradeCreateAll = "SalaryGrade.Create.All";
    public const string SalaryGradeUpdateAll = "SalaryGrade.Update.All";
    public const string SalaryGradeDeleteAll = "SalaryGrade.Delete.All";

    public const string AllowanceTypeReadAll = "AllowanceType.Read.All";
    public const string AllowanceTypeCreateAll = "AllowanceType.Create.All";
    public const string AllowanceTypeUpdateAll = "AllowanceType.Update.All";
    public const string AllowanceTypeDeleteAll = "AllowanceType.Delete.All";

    public const string ContractTemplateReadAll = "ContractTemplate.Read.All";
    public const string ContractTemplateCreateAll = "ContractTemplate.Create.All";
    public const string ContractTemplateUpdateAll = "ContractTemplate.Update.All";
    public const string ContractTemplateDeleteAll = "ContractTemplate.Delete.All";

    public const string ContractPartyReadAll = "ContractParty.Read.All";
    public const string ContractPartyCreateAll = "ContractParty.Create.All";

    // ===== Compliance module (FE-06) — IDs 65–88 =====

    public const string SocialInsuranceRateReadAll = "SocialInsuranceRate.Read.All";
    public const string SocialInsuranceRateCreateAll = "SocialInsuranceRate.Create.All";
    public const string SocialInsuranceRateUpdateAll = "SocialInsuranceRate.Update.All";
    public const string SocialInsuranceRateDeleteAll = "SocialInsuranceRate.Delete.All";

    public const string PitRegulationReadAll = "PitRegulation.Read.All";
    public const string PitRegulationCreateAll = "PitRegulation.Create.All";
    public const string PitRegulationUpdateAll = "PitRegulation.Update.All";
    public const string PitRegulationDeleteAll = "PitRegulation.Delete.All";

    public const string MinimumWageReadAll = "MinimumWage.Read.All";
    public const string MinimumWageCreateAll = "MinimumWage.Create.All";
    public const string MinimumWageUpdateAll = "MinimumWage.Update.All";
    public const string MinimumWageDeleteAll = "MinimumWage.Delete.All";

    public const string AllowanceExemptThresholdReadAll = "AllowanceExemptThreshold.Read.All";
    public const string AllowanceExemptThresholdCreateAll = "AllowanceExemptThreshold.Create.All";
    public const string AllowanceExemptThresholdUpdateAll = "AllowanceExemptThreshold.Update.All";
    public const string AllowanceExemptThresholdDeleteAll = "AllowanceExemptThreshold.Delete.All";

    // (81–84 reserved unused)

    public const string LaborLawConstantsReadAll = "LaborLawConstants.Read.All";
    public const string LaborLawConstantsCreateAll = "LaborLawConstants.Create.All";
    public const string LaborLawConstantsUpdateAll = "LaborLawConstants.Update.All";
    public const string LaborLawConstantsDeleteAll = "LaborLawConstants.Delete.All";

    // ===== AuditLog + LegalDocument + Compliance Auto-Update — IDs 89–94 =====

    public const string AuditLogReadAll = "AuditLog.Read.All";

    public const string LegalDocumentReadAll = "LegalDocument.Read.All";
    public const string LegalDocumentWriteAll = "LegalDocument.Write.All";

    public const string ComplianceAutoUpdateRunAll = "ComplianceAutoUpdate.Run.All";
    public const string ComplianceAutoUpdateReadAll = "ComplianceAutoUpdate.Read.All";
    public const string ComplianceAutoUpdateApproveAll = "ComplianceAutoUpdate.Approve.All";

    // ===== Attendance module (FE-ATT) — IDs 95–129 =====
    // Note: EmployeeShiftReadSelf thêm sau ở ID 179 (100 đã bị EmployeeShiftCreateAll chiếm).

    public const string ShiftTemplateReadAll = "ShiftTemplate.Read.All";
    public const string ShiftTemplateCreateAll = "ShiftTemplate.Create.All";
    public const string ShiftTemplateUpdateAll = "ShiftTemplate.Update.All";
    public const string ShiftTemplateDeleteAll = "ShiftTemplate.Delete.All";

    public const string EmployeeShiftReadAll = "EmployeeShift.Read.All";
    public const string EmployeeShiftCreateAll = "EmployeeShift.Create.All";
    public const string EmployeeShiftDeleteAll = "EmployeeShift.Delete.All";

    public const string PublicHolidayReadAll = "PublicHoliday.Read.All";
    public const string PublicHolidayCreateAll = "PublicHoliday.Create.All";
    public const string PublicHolidayUpdateAll = "PublicHoliday.Update.All";
    public const string PublicHolidayDeleteAll = "PublicHoliday.Delete.All";

    public const string AttendanceReadAll = "Attendance.Read.All";
    public const string AttendanceReadSelf = "Attendance.Read.Self";
    public const string AttendanceCreateAll = "Attendance.Create.All";
    public const string AttendanceUpdateAll = "Attendance.Update.All";

    public const string OvertimeRequestReadAll = "OvertimeRequest.Read.All";
    public const string OvertimeRequestReadSelf = "OvertimeRequest.Read.Self";
    /// <summary>
    /// DEPRECATED — luồng OT giờ do PM tạo batch, NV không tự xin.
    /// Constant giữ lại để migration cũ không vỡ, KHÔNG cấp cho role nào.
    /// </summary>
    public const string OvertimeRequestCreateSelf = "OvertimeRequest.Create.Self";
    public const string OvertimeRequestApproveAll = "OvertimeRequest.Approve.All";

    public const string LeaveRequestReadAll = "LeaveRequest.Read.All";
    public const string LeaveRequestReadSelf = "LeaveRequest.Read.Self";
    public const string LeaveRequestCreateSelf = "LeaveRequest.Create.Self";
    public const string LeaveRequestApproveAll = "LeaveRequest.Approve.All";

    public const string AttendanceAdjustmentReadAll = "AttendanceAdjustment.Read.All";
    public const string AttendanceAdjustmentCreateSelf = "AttendanceAdjustment.Create.Self";
    public const string AttendanceAdjustmentApproveAll = "AttendanceAdjustment.Approve.All";
    public const string AttendanceAdjustmentOverrideAll = "AttendanceAdjustment.Override.All";

    public const string AttendanceRuleReadAll = "AttendanceRule.Read.All";
    public const string AttendanceRuleCreateAll = "AttendanceRule.Create.All";
    public const string AttendanceRuleUpdateAll = "AttendanceRule.Update.All";
    public const string AttendanceRuleDeleteAll = "AttendanceRule.Delete.All";

    public const string LeaveTypeReadAll = "LeaveType.Read.All";
    public const string LeaveTypeCreateAll = "LeaveType.Create.All";
    public const string LeaveTypeUpdateAll = "LeaveType.Update.All";
    public const string LeaveTypeDeleteAll = "LeaveType.Delete.All";

    // ===== Company Policy module — IDs 130–133 =====

    public const string CompanyPolicyReadAll = "CompanyPolicy.Read.All";
    public const string CompanyPolicyCreateAll = "CompanyPolicy.Create.All";
    public const string CompanyPolicyUpdateAll = "CompanyPolicy.Update.All";
    public const string CompanyPolicyDeleteAll = "CompanyPolicy.Delete.All";

    // ===== FaceRegistration (IDs 134–136) — retired: module gỡ ở nhánh remove/face-attendance =====
    // ===== OvertimeSlotTemplate (IDs 137–140) — retired: module gỡ ở nhánh dev =====
    // IDs 134–140 giữ trống cho audit continuity (không tái sử dụng).

    // ===== AttendanceSetting (Attendance ext) — IDs 141–142 =====

    public const string AttendanceSettingReadAll = "AttendanceSetting.Read.All";
    public const string AttendanceSettingUpdateAll = "AttendanceSetting.Update.All";

    // ===== Department module — IDs 143–147 =====

    public const string DepartmentReadAll   = "Department.Read.All";
    public const string DepartmentCreateAll = "Department.Create.All";
    public const string DepartmentUpdateAll = "Department.Update.All";
    public const string DepartmentDeleteAll = "Department.Delete.All";

    public const string EmployeeInformationReadDepartment = "EmployeeInformation.Read.Department";

    // ===== Task/Production module (v5) — IDs 148–169 =====
    // ProcessTemplate (quy trình mẫu) — GĐ định nghĩa dùng chung; PM đọc để áp.

    public const string ProcessTemplateReadAll   = "ProcessTemplate.Read.All";
    public const string ProcessTemplateCreateAll = "ProcessTemplate.Create.All";
    public const string ProcessTemplateUpdateAll = "ProcessTemplate.Update.All";
    public const string ProcessTemplateDeleteAll = "ProcessTemplate.Delete.All";

    // Project/Order (đơn hàng)
    public const string ProjectReadAll          = "Project.Read.All";
    public const string ProjectReadDepartment   = "Project.Read.Department";
    public const string ProjectCreateAll        = "Project.Create.All";
    public const string ProjectUpdateAll        = "Project.Update.All";
    public const string ProjectDeleteAll        = "Project.Delete.All";
    public const string ProjectGenerateTasks    = "Project.GenerateTasks";
    public const string ProjectCompleteDepartment = "Project.Complete.Department"; // đóng đơn thủ công

    // Task — xem theo scope
    public const string TaskReadAll          = "Task.Read.All";
    public const string TaskReadDepartment   = "Task.Read.Department";
    public const string TaskReadAssigned     = "Task.Read.Assigned";
    public const string TaskReadConfidential = "Task.Read.Confidential";

    // Task — thao tác (PM xưởng; nhân viên KHÔNG có quyền ghi task nào)
    public const string TaskCreateDepartment      = "Task.Create.Department";        // tạo task lá 1 người/nhóm (PM)
    public const string TaskCreateConfidential    = "Task.Create.Confidential";
    public const string TaskUpdateDepartment      = "Task.Update.Department";
    public const string TaskUpdateProgressDepartment = "Task.UpdateProgress.Department"; // cập nhật CompletedQuantity
    public const string TaskCompleteDepartment    = "Task.Complete.Department";      // bấm hoàn thành (+ WasLate)
    public const string TaskDeleteDepartment      = "Task.Delete.Department";

    // Attachment (tài liệu/ảnh) — LEGACY, giữ để tương thích ngược. Không seed cho role nào nữa —
    // dùng Read/Write.Project (tầng 1) và Read/Write.Stage (tầng 2+3) thay thế ở IDs 220–223.
    public const string ProductionAttachmentReadDepartment  = "ProductionAttachment.Read.Department";

    // (170–177 reserved unused — WeeklyDayOff module removed 2026-07-10)

    // ===== OT batch workflow + EmployeeShift self-read — IDs 178–179 =====

    /// <summary>PM tạo batch OT request cho NV cùng phòng ban (project scope).</summary>
    public const string OvertimeRequestCreateBatchDepartment = "OvertimeRequest.CreateBatch.Department";

    /// <summary>NV xem lịch phân ca kế hoạch của chính mình.</summary>
    public const string EmployeeShiftReadSelf = "EmployeeShift.Read.Self";

    // (180–204 reserved unused)

    // ===== Task/Production extensions — IDs 205–213 =====

    public const string ProductionAttachmentWriteDepartment = "ProductionAttachment.Write.Department"; // LEGACY
    public const string ProductionAttachmentReadAll         = "ProductionAttachment.Read.All";         // LEGACY

    // Audit — xem lịch sử chỉnh sửa task/đơn (scoped theo xưởng)
    public const string TaskAuditReadAll        = "Task.Audit.Read.All";
    public const string TaskAuditReadDepartment = "Task.Audit.Read.Department";

    // Task v7 (tách Create theo IsBatchParent) + Comment module.
    // Quyền xem/viết đi theo entity gốc (Project/Task) — CHỈ Employee cần permission riêng
    // vì đây là ngoại lệ duy nhất phá vỡ "Employee không có quyền ghi" (plan ràng buộc 6).
    public const string TaskCreateStageDepartment = "Task.CreateStage.Department";   // tạo Công đoạn (Manager/GĐ)
    public const string CommentReadAll           = "Comment.Read.All";               // GĐ/Phó GĐ — mọi tầng, mọi xưởng
    public const string CommentCreateOwn         = "Comment.Create.Own";             // Employee — CHỈ Task của chính mình
    public const string CommentCreateDepartment  = "Comment.Create.Department";      // Manager/PM — theo scope

    /// <summary>
    /// Chỉ cho phép ĐỌC quy trình mẫu để chọn khi "Áp quy trình" (GenerateTasksModal).
    /// KHÔNG cho vào màn quản lý/CRUD quy trình mẫu (vẫn cần ProcessTemplateReadAll).
    /// </summary>
    public const string ProcessTemplateReadForApply = "ProcessTemplate.ReadForApply.All";

    // (214–217 reserved unused)

    // ===== Task/Production full-department reads — IDs 218–219 =====
    // Xem TOÀN BỘ đơn/công đoạn/task của 1 xưởng (3 tầng, bỏ qua filter theo AssignedPmUserId).
    // Dành cho vai trò quản lý cấp xưởng không trực tiếp phụ trách công đoạn (vd. DepartmentManager).

    public const string ProjectReadFullDepartment = "Project.Read.FullDepartment";
    public const string TaskReadFullDepartment    = "Task.Read.FullDepartment";

    // ===== ProductionAttachment tier split — IDs 220–223 =====
    // Tách theo tầng: Project = Đơn hàng (tầng 1, Director+DepartmentManager);
    // Stage = Công đoạn/Task lá gộp chung (tầng 2+3, quyền xem/ghi giống nhau).

    // Tầng 1 (Đơn hàng) — chỉ Director/DeputyDirector/DepartmentManager.
    public const string ProductionAttachmentReadProject  = "ProductionAttachment.Read.Project";
    public const string ProductionAttachmentWriteProject = "ProductionAttachment.Write.Project";

    // Tầng 2+3 (Công đoạn/Task lá) — Director/DeputyDirector/DepartmentManager/ProjectManager ghi được;
    // Employee chỉ có Read (xem tài liệu mẫu của Công đoạn/task được giao, không đăng tải).
    public const string ProductionAttachmentReadStage  = "ProductionAttachment.Read.Stage";
    public const string ProductionAttachmentWriteStage = "ProductionAttachment.Write.Stage";
}

public static class PermissionIds
{
    // ===== Core: User + EmployeeInformation + Position + Metric + Role + RefreshToken — IDs 1–32 =====

    public const int UserReadAll = 1;
    public const int UserReadSelf = 2;
    public const int UserCreateAll = 3;
    public const int UserUpdateAll = 4;
    public const int UserUpdateSelf = 5;
    public const int UserDeleteAll = 6;

    public const int EmployeeInformationReadAll = 7;
    public const int EmployeeInformationReadSelf = 8;
    public const int EmployeeInformationCreateAll = 9;
    public const int EmployeeInformationUpdateAll = 10;
    public const int EmployeeInformationUpdateSelf = 11;
    public const int EmployeeInformationDeleteAll = 12;

    public const int PositionReadAll = 13;
    public const int PositionCreateAll = 14;
    public const int PositionUpdateAll = 15;
    public const int PositionDeleteAll = 16;

    public const int PerformanceMetricReadAll = 17;
    public const int PerformanceMetricReadSelf = 18;
    public const int PerformanceMetricCreateAll = 19;
    public const int PerformanceMetricUpdateAll = 20;
    public const int PerformanceMetricDeleteAll = 21;
    public const int PerformanceMetricApproveAll = 22;

    public const int TypePerformanceMetricReadAll = 23;
    public const int TypePerformanceMetricCreateAll = 24;
    public const int TypePerformanceMetricUpdateAll = 25;
    public const int TypePerformanceMetricDeleteAll = 26;

    public const int RoleReadAll = 27;
    public const int RoleCreateAll = 28;
    public const int RoleUpdateAll = 29;
    public const int RoleDeleteAll = 30;
    public const int RoleManagePermissionsAll = 31;

    public const int RefreshTokenRevokeAll = 32;

    // ===== Contract module (Phase 1) — IDs 33–64 =====

    public const int ContractReadAll = 33;
    public const int ContractReadSelf = 34;
    public const int ContractCreateAll = 35;
    public const int ContractUpdateAll = 36;
    public const int ContractDeleteAll = 37;
    public const int ContractActivateAll = 38;
    public const int ContractCancelAll = 39;

    public const int ContractAppendixReadAll = 40;
    public const int ContractAppendixReadSelf = 41;
    public const int ContractAppendixCreateAll = 42;
    public const int ContractAppendixActivateAll = 43;

    public const int ContractTerminationReadAll = 44;
    public const int ContractTerminationReadSelf = 45;
    public const int ContractTerminationCreateAll = 46;

    public const int ContractFileReadAll = 47;
    public const int ContractFileReadSelf = 48;
    public const int ContractFileUploadAll = 49;
    public const int ContractFileDeleteAll = 50;

    public const int SalaryGradeReadAll = 51;
    public const int SalaryGradeCreateAll = 52;
    public const int SalaryGradeUpdateAll = 53;
    public const int SalaryGradeDeleteAll = 54;

    public const int AllowanceTypeReadAll = 55;
    public const int AllowanceTypeCreateAll = 56;
    public const int AllowanceTypeUpdateAll = 57;
    public const int AllowanceTypeDeleteAll = 58;

    public const int ContractTemplateReadAll = 59;
    public const int ContractTemplateCreateAll = 60;
    public const int ContractTemplateUpdateAll = 61;
    public const int ContractTemplateDeleteAll = 62;

    public const int ContractPartyReadAll = 63;
    public const int ContractPartyCreateAll = 64;

    // ===== Compliance module (FE-06) — IDs 65–88 =====

    public const int SocialInsuranceRateReadAll = 65;
    public const int SocialInsuranceRateCreateAll = 66;
    public const int SocialInsuranceRateUpdateAll = 67;
    public const int SocialInsuranceRateDeleteAll = 68;

    public const int PitRegulationReadAll = 69;
    public const int PitRegulationCreateAll = 70;
    public const int PitRegulationUpdateAll = 71;
    public const int PitRegulationDeleteAll = 72;

    public const int MinimumWageReadAll = 73;
    public const int MinimumWageCreateAll = 74;
    public const int MinimumWageUpdateAll = 75;
    public const int MinimumWageDeleteAll = 76;

    public const int AllowanceExemptThresholdReadAll = 77;
    public const int AllowanceExemptThresholdCreateAll = 78;
    public const int AllowanceExemptThresholdUpdateAll = 79;
    public const int AllowanceExemptThresholdDeleteAll = 80;

    // (81–84 reserved unused)

    public const int LaborLawConstantsReadAll = 85;
    public const int LaborLawConstantsCreateAll = 86;
    public const int LaborLawConstantsUpdateAll = 87;
    public const int LaborLawConstantsDeleteAll = 88;

    // ===== AuditLog + LegalDocument + Compliance Auto-Update — IDs 89–94 =====

    public const int AuditLogReadAll = 89;

    public const int LegalDocumentReadAll = 90;
    public const int LegalDocumentWriteAll = 91;

    public const int ComplianceAutoUpdateRunAll = 92;
    public const int ComplianceAutoUpdateReadAll = 93;
    public const int ComplianceAutoUpdateApproveAll = 94;

    // ===== Attendance module (FE-ATT) — IDs 95–129 =====
    // Note: EmployeeShiftReadSelf thêm sau ở ID 179 (100 đã bị EmployeeShiftCreateAll chiếm).

    public const int ShiftTemplateReadAll = 95;
    public const int ShiftTemplateCreateAll = 96;
    public const int ShiftTemplateUpdateAll = 97;
    public const int ShiftTemplateDeleteAll = 98;

    public const int EmployeeShiftReadAll = 99;
    public const int EmployeeShiftCreateAll = 100;
    public const int EmployeeShiftDeleteAll = 101;

    public const int PublicHolidayReadAll = 102;
    public const int PublicHolidayCreateAll = 103;
    public const int PublicHolidayUpdateAll = 104;
    public const int PublicHolidayDeleteAll = 105;

    public const int AttendanceReadAll = 106;
    public const int AttendanceReadSelf = 107;
    public const int AttendanceCreateAll = 108;
    public const int AttendanceUpdateAll = 109;

    public const int OvertimeRequestReadAll = 110;
    public const int OvertimeRequestReadSelf = 111;
    public const int OvertimeRequestCreateSelf = 112; // DEPRECATED — không seed cho role nào
    public const int OvertimeRequestApproveAll = 113;

    public const int LeaveRequestReadAll = 114;
    public const int LeaveRequestReadSelf = 115;
    public const int LeaveRequestCreateSelf = 116;
    public const int LeaveRequestApproveAll = 117;

    public const int AttendanceAdjustmentReadAll = 118;
    public const int AttendanceAdjustmentCreateSelf = 119;
    public const int AttendanceAdjustmentApproveAll = 120;
    public const int AttendanceAdjustmentOverrideAll = 121;

    public const int AttendanceRuleReadAll = 122;
    public const int AttendanceRuleCreateAll = 123;
    public const int AttendanceRuleUpdateAll = 124;
    public const int AttendanceRuleDeleteAll = 125;

    public const int LeaveTypeReadAll = 126;
    public const int LeaveTypeCreateAll = 127;
    public const int LeaveTypeUpdateAll = 128;
    public const int LeaveTypeDeleteAll = 129;

    // ===== Company Policy module — IDs 130–133 =====

    public const int CompanyPolicyReadAll = 130;
    public const int CompanyPolicyCreateAll = 131;
    public const int CompanyPolicyUpdateAll = 132;
    public const int CompanyPolicyDeleteAll = 133;

    // ===== FaceRegistration (IDs 134–136) — retired: module gỡ ở nhánh remove/face-attendance =====
    // ===== OvertimeSlotTemplate (IDs 137–140) — retired: module gỡ ở nhánh dev =====
    // IDs 134–140 giữ trống cho audit continuity (không tái sử dụng).

    // ===== AttendanceSetting (Attendance ext) — IDs 141–142 =====

    public const int AttendanceSettingReadAll = 141;
    public const int AttendanceSettingUpdateAll = 142;

    // ===== Department module — IDs 143–147 =====

    public const int DepartmentReadAll   = 143;
    public const int DepartmentCreateAll = 144;
    public const int DepartmentUpdateAll = 145;
    public const int DepartmentDeleteAll = 146;

    public const int EmployeeInformationReadDepartment = 147;

    // ===== Task/Production module (v5) — IDs 148–169 =====

    public const int ProcessTemplateReadAll     = 148;
    public const int ProcessTemplateCreateAll   = 149;
    public const int ProcessTemplateUpdateAll   = 150;
    public const int ProcessTemplateDeleteAll   = 151;

    public const int ProjectReadAll             = 152;
    public const int ProjectReadDepartment      = 153;
    public const int ProjectCreateAll           = 154;
    public const int ProjectUpdateAll           = 155;
    public const int ProjectDeleteAll           = 156;
    public const int ProjectGenerateTasks       = 157;
    public const int ProjectCompleteDepartment  = 158;

    public const int TaskReadAll                = 159;
    public const int TaskReadDepartment         = 160;
    public const int TaskReadAssigned           = 161;
    public const int TaskReadConfidential       = 162;

    public const int TaskCreateDepartment          = 163;
    public const int TaskCreateConfidential        = 164;
    public const int TaskUpdateDepartment          = 165;
    public const int TaskUpdateProgressDepartment  = 166;
    public const int TaskCompleteDepartment        = 167;
    public const int TaskDeleteDepartment          = 168;

    public const int ProductionAttachmentReadDepartment  = 169; // LEGACY

    // (170–177 reserved unused — WeeklyDayOff module removed 2026-07-10)

    // ===== OT batch workflow + EmployeeShift self-read — IDs 178–179 =====

    public const int OvertimeRequestCreateBatchDepartment = 178;
    public const int EmployeeShiftReadSelf = 179;

    // (180–204 reserved unused)

    // ===== Task/Production extensions — IDs 205–213 =====

    public const int ProductionAttachmentWriteDepartment = 205; // LEGACY
    public const int ProductionAttachmentReadAll         = 206; // LEGACY

    public const int TaskAuditReadAll           = 207;
    public const int TaskAuditReadDepartment    = 208;

    public const int TaskCreateStageDepartment  = 209;
    public const int CommentReadAll             = 210;
    public const int CommentCreateOwn           = 211;
    public const int CommentCreateDepartment    = 212;

    public const int ProcessTemplateReadForApply = 213;

    // (214–217 reserved unused)

    // ===== Task/Production full-department reads — IDs 218–219 =====

    public const int ProjectReadFullDepartment = 218;
    public const int TaskReadFullDepartment    = 219;

    // ===== ProductionAttachment tier split — IDs 220–223 =====

    public const int ProductionAttachmentReadProject   = 220;
    public const int ProductionAttachmentWriteProject  = 221;
    public const int ProductionAttachmentReadStage     = 222;
    public const int ProductionAttachmentWriteStage    = 223;
}
