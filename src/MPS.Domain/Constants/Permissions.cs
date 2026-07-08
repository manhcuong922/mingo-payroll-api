namespace MPS.Domain.Constants;

public static class Permissions
{
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

    // ===== Contract module (Phase 1) =====

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

    // ===== Compliance module (FE-06) — IDs 65–94 =====

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

    public const string LaborLawConstantsReadAll = "LaborLawConstants.Read.All";
    public const string LaborLawConstantsCreateAll = "LaborLawConstants.Create.All";
    public const string LaborLawConstantsUpdateAll = "LaborLawConstants.Update.All";
    public const string LaborLawConstantsDeleteAll = "LaborLawConstants.Delete.All";

    // ===== Audit Log module (FT-39) — System Admin only =====

    public const string AuditLogReadAll = "AuditLog.Read.All";
    // ===== LegalDocument module — IDs 90–91 =====

    public const string LegalDocumentReadAll = "LegalDocument.Read.All";
    public const string LegalDocumentWriteAll = "LegalDocument.Write.All";

    // ===== Compliance Auto-Update module (crawl + AI đề xuất) =====

    public const string ComplianceAutoUpdateRunAll = "ComplianceAutoUpdate.Run.All";
    public const string ComplianceAutoUpdateReadAll = "ComplianceAutoUpdate.Read.All";
    public const string ComplianceAutoUpdateApproveAll = "ComplianceAutoUpdate.Approve.All";

    // ===== Attendance module (FE-ATT) — IDs 95–129 =====

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
    public const string AttendanceSettingReadAll = "AttendanceSetting.Read.All";
    public const string AttendanceSettingUpdateAll = "AttendanceSetting.Update.All";

    public const string OvertimeRequestReadAll = "OvertimeRequest.Read.All";
    public const string OvertimeRequestReadSelf = "OvertimeRequest.Read.Self";
    public const string OvertimeRequestCreateSelf = "OvertimeRequest.Create.Self";
    public const string OvertimeRequestApproveAll = "OvertimeRequest.Approve.All";

    public const string OvertimeSlotTemplateReadAll = "OvertimeSlotTemplate.Read.All";
    public const string OvertimeSlotTemplateCreateAll = "OvertimeSlotTemplate.Create.All";
    public const string OvertimeSlotTemplateUpdateAll = "OvertimeSlotTemplate.Update.All";
    public const string OvertimeSlotTemplateDeleteAll = "OvertimeSlotTemplate.Delete.All";

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
    // ===== FaceRegistration (Track B) =====
    public const string FaceRegistrationSubmitSelf = "FaceRegistration.Submit.Self";
    public const string FaceRegistrationReadAll    = "FaceRegistration.Read.All";
    public const string FaceRegistrationApproveAll = "FaceRegistration.Approve.All";
    // ===== Department module - IDs 143-147 =====

    public const string DepartmentReadAll   = "Department.Read.All";
    public const string DepartmentCreateAll = "Department.Create.All";
    public const string DepartmentUpdateAll = "Department.Update.All";
    public const string DepartmentDeleteAll = "Department.Delete.All";

    public const string EmployeeInformationReadDepartment = "EmployeeInformation.Read.Department";

    // ===== Task/Production module (v5) - IDs 148-173 =====

    // ProcessTemplate (quy trình mẫu) — GĐ định nghĩa dùng chung; PM đọc để áp
    public const string ProcessTemplateReadAll   = "ProcessTemplate.Read.All";
    public const string ProcessTemplateCreateAll = "ProcessTemplate.Create.All";
    public const string ProcessTemplateUpdateAll = "ProcessTemplate.Update.All";
    public const string ProcessTemplateDeleteAll = "ProcessTemplate.Delete.All";

    // Project/Order (đơn hàng)
    public const string ProjectReadAll          = "Project.Read.All";
    public const string ProjectReadDepartment   = "Project.Read.Department";
    // Xem TOÀN BỘ đơn/công đoạn/task của 1 xưởng (3 tầng, bỏ qua lọc theo AssignedPmUserId) — khác
    // ProjectReadDepartment (PM: chỉ công đoạn được gán + task con, xem FilterByAssignedPm). Dành cho
    // vai trò quản lý cấp xưởng không trực tiếp phụ trách công đoạn nào (vd. DepartmentManager).
    public const string ProjectReadFullDepartment = "Project.Read.FullDepartment";
    public const string ProjectCreateAll        = "Project.Create.All";
    public const string ProjectUpdateAll        = "Project.Update.All";
    public const string ProjectDeleteAll        = "Project.Delete.All";
    public const string ProjectGenerateTasks    = "Project.GenerateTasks";
    public const string ProjectCompleteDepartment = "Project.Complete.Department";  // ⭐ đóng đơn thủ công

    // Task — xem theo scope
    public const string TaskReadAll          = "Task.Read.All";
    public const string TaskReadDepartment   = "Task.Read.Department";
    // Song song ProjectReadFullDepartment: xem mọi Công đoạn + task con trong xưởng, không bị ẩn theo
    // AssignedPmUserId (khác TaskReadDepartment, vốn bị FilterByAssignedPm giới hạn cho PM).
    public const string TaskReadFullDepartment = "Task.Read.FullDepartment";
    public const string TaskReadAssigned     = "Task.Read.Assigned";
    public const string TaskReadConfidential = "Task.Read.Confidential";

    // Task — thao tác (PM xưởng; nhân viên KHÔNG có quyền ghi task nào)
    public const string TaskCreateDepartment      = "Task.Create.Department";        // ⭐ tạo task lá 1 người / nhóm (PM)
    public const string TaskCreateStageDepartment = "Task.CreateStage.Department";   // ⭐ tạo Công đoạn (Manager/GĐ)
    public const string TaskCreateConfidential    = "Task.Create.Confidential";
    public const string TaskUpdateDepartment      = "Task.Update.Department";
    public const string TaskUpdateProgressDepartment = "Task.UpdateProgress.Department"; // ⭐ cập nhật CompletedQuantity
    public const string TaskCompleteDepartment    = "Task.Complete.Department";      // ⭐ bấm hoàn thành (+ WasLate)
    public const string TaskDeleteDepartment      = "Task.Delete.Department";

    // Attachment (tài liệu/ảnh) — cũ, giữ để tương thích ngược, không seed cho role nào nữa (xem
    // Read/Write.Project và Read/Write.Stage thay thế, tách theo tầng: Project = Đơn hàng (tầng 1,
    // Director+Manager); Stage = Công đoạn/Task lá gộp chung (tầng 2+3, quyền xem/ghi giống nhau).
    public const string ProductionAttachmentReadDepartment  = "ProductionAttachment.Read.Department";
    public const string ProductionAttachmentWriteDepartment = "ProductionAttachment.Write.Department";
    public const string ProductionAttachmentReadAll         = "ProductionAttachment.Read.All";

    // Tầng 1 (Đơn hàng) — chỉ Director/DeputyDirector/DepartmentManager.
    public const string ProductionAttachmentReadProject  = "ProductionAttachment.Read.Project";
    public const string ProductionAttachmentWriteProject = "ProductionAttachment.Write.Project";
    // Tầng 2+3 (Công đoạn/Task lá) — Director/DeputyDirector/DepartmentManager/ProjectManager ghi được;
    // Employee chỉ có Read (xem tài liệu mẫu của Công đoạn/task được giao, không đăng tải).
    public const string ProductionAttachmentReadStage  = "ProductionAttachment.Read.Stage";
    public const string ProductionAttachmentWriteStage = "ProductionAttachment.Write.Stage";

    // Audit — xem lịch sử chỉnh sửa task/đơn (scoped theo xưởng)
    public const string TaskAuditReadAll        = "Task.Audit.Read.All";
    public const string TaskAuditReadDepartment = "Task.Audit.Read.Department";

    // ===== Comment module (v7, mục 5/8 plan) - IDs 174-176 =====
    // Quyền xem/viết đi theo đúng quyền của entity gốc (Project/Task) — CHỈ Employee cần permission riêng
    // vì đây là ngoại lệ duy nhất phá vỡ "Employee không có quyền ghi" (plan ràng buộc 6).
    public const string CommentReadAll           = "Comment.Read.All";           // GĐ/Phó GĐ — mọi tầng, mọi xưởng
    public const string CommentCreateOwn         = "Comment.Create.Own";        // Employee — CHỈ Task của chính mình
    public const string CommentCreateDepartment  = "Comment.Create.Department"; // Manager/PM — theo scope đang có trên entity gốc

    // ===== ID 213 =====
    // Tách khỏi ProcessTemplateReadAll: chỉ cho phép ĐỌC quy trình mẫu để chọn khi "Áp quy trình"
    // (GenerateTasksModal) — KHÔNG cho vào màn quản lý/CRUD quy trình mẫu (đó vẫn cần ProcessTemplateReadAll
    // hoặc ProcessTemplateCreateAll). PM cần quyền này để sinh task từ quy trình mẫu (Project.GenerateTasks)
    // nhưng không được tự tạo/sửa/xóa quy trình mẫu dùng chung toàn công ty.
    public const string ProcessTemplateReadForApply = "ProcessTemplate.ReadForApply.All";
}

public static class PermissionIds
{
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

    // ===== Contract module (Phase 1) — IDs 33–64 (reserved range 33–100) =====

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

    // ===== Compliance module (FE-06) — IDs 65–94 =====

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

    public const int LaborLawConstantsReadAll = 85;
    public const int LaborLawConstantsCreateAll = 86;
    public const int LaborLawConstantsUpdateAll = 87;
    public const int LaborLawConstantsDeleteAll = 88;

    // ===== LegalDocument module — ID 89 (đã seed trong DB trước AuditLog) =====

    public const int AuditLogReadAll = 89;

    // ===== LegalDocument module — IDs 90–91 =====

    public const int LegalDocumentReadAll = 90;
    public const int LegalDocumentWriteAll = 91;

    // ===== Compliance Auto-Update module — IDs 92–94 =====

    public const int ComplianceAutoUpdateRunAll = 92;
    public const int ComplianceAutoUpdateReadAll = 93;
    public const int ComplianceAutoUpdateApproveAll = 94;

    // ===== Attendance module (FE-ATT) — IDs 95–129 =====

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
    public const int OvertimeRequestCreateSelf = 112;
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
    // ===== FaceRegistration (Track B) — IDs 134–136 =====
    public const int FaceRegistrationSubmitSelf = 134;
    public const int FaceRegistrationReadAll    = 135;
    public const int FaceRegistrationApproveAll = 136;

    // ===== OvertimeSlotTemplate (Attendance ext) — IDs 137–140 =====
    public const int OvertimeSlotTemplateReadAll = 137;
    public const int OvertimeSlotTemplateCreateAll = 138;
    public const int OvertimeSlotTemplateUpdateAll = 139;
    public const int OvertimeSlotTemplateDeleteAll = 140;

    // ===== AttendanceSetting (Attendance ext) — IDs 141–142 =====
    public const int AttendanceSettingReadAll = 141;
    public const int AttendanceSettingUpdateAll = 142;
    // ===== Department module - IDs 143-147 =====

    public const int DepartmentReadAll   = 143;
    public const int DepartmentCreateAll = 144;
    public const int DepartmentUpdateAll = 145;
    public const int DepartmentDeleteAll = 146;

    public const int EmployeeInformationReadDepartment = 147;

    // ===== Task/Production module (v5) - IDs 148-169, 205-208 =====
    // (170-204 dùng bởi AttendanceModule/ShiftTemplate/PublicHoliday/LeaveType — xem migration đã áp dụng cho DB)
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

    public const int ProductionAttachmentReadDepartment  = 169;
    public const int ProductionAttachmentWriteDepartment = 205;
    public const int ProductionAttachmentReadAll         = 206;

    public const int TaskAuditReadAll           = 207;
    public const int TaskAuditReadDepartment    = 208;

    // ===== Task v7 (tách Create theo IsBatchParent) + Comment module - IDs 209-212 =====
    public const int TaskCreateStageDepartment  = 209;
    public const int CommentReadAll             = 210;
    public const int CommentCreateOwn           = 211;
    public const int CommentCreateDepartment    = 212;

    // ===== ID 213 =====
    public const int ProcessTemplateReadForApply = 213;

    // Chú ý: IDs 214-217 đã bị chiếm bởi WeeklyDayOff module (migration 20260707110000_AddWeeklyDayOffRules,
    // seed qua raw SQL — không xuất hiện trong file này). Dùng 218-219 để tránh trùng khóa chính.
    // ===== IDs 218-219: xem toàn bộ 1 xưởng (3 tầng), không giới hạn theo AssignedPmUserId =====
    public const int ProjectReadFullDepartment = 218;
    public const int TaskReadFullDepartment    = 219;

    // ===== IDs 220-223: tài liệu/ảnh tách theo tầng (Project = tầng 1, Stage = tầng 2+3) =====
    public const int ProductionAttachmentReadProject   = 220;
    public const int ProductionAttachmentWriteProject  = 221;
    public const int ProductionAttachmentReadStage     = 222;
    public const int ProductionAttachmentWriteStage    = 223;
}
