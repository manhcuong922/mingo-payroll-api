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
    // ===== LegalDocument module — IDs 89–90 =====

    public const string LegalDocumentReadAll = "LegalDocument.Read.All";
    public const string LegalDocumentWriteAll = "LegalDocument.Write.All";

    // ===== Compliance Auto-Update module (crawl + AI đề xuất) =====

    public const string ComplianceAutoUpdateRunAll = "ComplianceAutoUpdate.Run.All";
    public const string ComplianceAutoUpdateReadAll = "ComplianceAutoUpdate.Read.All";
    public const string ComplianceAutoUpdateApproveAll = "ComplianceAutoUpdate.Approve.All";

    // ===== Department module — IDs 95–99 =====

    public const string DepartmentReadAll   = "Department.Read.All";
    public const string DepartmentCreateAll = "Department.Create.All";
    public const string DepartmentUpdateAll = "Department.Update.All";
    public const string DepartmentDeleteAll = "Department.Delete.All";

    public const string EmployeeInformationReadDepartment = "EmployeeInformation.Read.Department";

    // ===== Task/Production module (v5) — IDs 100–125 =====

    // ProcessTemplate (quy trình mẫu) — GĐ định nghĩa dùng chung; PM đọc để áp
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
    public const string ProjectCompleteDepartment = "Project.Complete.Department";  // ⭐ đóng đơn thủ công

    // Task — xem theo scope
    public const string TaskReadAll          = "Task.Read.All";
    public const string TaskReadDepartment   = "Task.Read.Department";
    public const string TaskReadAssigned     = "Task.Read.Assigned";
    public const string TaskReadConfidential = "Task.Read.Confidential";

    // Task — thao tác (PM xưởng; nhân viên KHÔNG có quyền ghi task nào)
    public const string TaskCreateDepartment      = "Task.Create.Department";        // ⭐ tạo task 1 người / nhóm
    public const string TaskCreateConfidential    = "Task.Create.Confidential";
    public const string TaskUpdateDepartment      = "Task.Update.Department";
    public const string TaskUpdateProgressDepartment = "Task.UpdateProgress.Department"; // ⭐ cập nhật CompletedQuantity
    public const string TaskCompleteDepartment    = "Task.Complete.Department";      // ⭐ bấm hoàn thành (+ WasLate)
    public const string TaskDeleteDepartment      = "Task.Delete.Department";

    // Attachment (tài liệu/ảnh)
    public const string ProductionAttachmentReadDepartment  = "ProductionAttachment.Read.Department";
    public const string ProductionAttachmentWriteDepartment = "ProductionAttachment.Write.Department";
    public const string ProductionAttachmentReadAll         = "ProductionAttachment.Read.All";

    // Audit — xem lịch sử chỉnh sửa task/đơn (scoped theo xưởng)
    public const string TaskAuditReadAll        = "Task.Audit.Read.All";
    public const string TaskAuditReadDepartment = "Task.Audit.Read.Department";
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

    public const int LegalDocumentReadAll = 89;
    public const int LegalDocumentWriteAll = 91;

    // ===== Audit Log module (FT-39) — ID 90 =====

    public const int AuditLogReadAll = 90;

    // ===== Compliance Auto-Update module — IDs 92–93 =====

    public const int ComplianceAutoUpdateRunAll = 92;
    public const int ComplianceAutoUpdateReadAll = 93;
    public const int ComplianceAutoUpdateApproveAll = 94;

    // ===== Department module — IDs 95–99 =====

    public const int DepartmentReadAll   = 95;
    public const int DepartmentCreateAll = 96;
    public const int DepartmentUpdateAll = 97;
    public const int DepartmentDeleteAll = 98;

    public const int EmployeeInformationReadDepartment = 99;

    // ===== Task/Production module (v5) — IDs 100–125 =====
    public const int ProcessTemplateReadAll     = 100;
    public const int ProcessTemplateCreateAll   = 101;
    public const int ProcessTemplateUpdateAll   = 102;
    public const int ProcessTemplateDeleteAll   = 103;

    public const int ProjectReadAll             = 104;
    public const int ProjectReadDepartment      = 105;
    public const int ProjectCreateAll           = 106;
    public const int ProjectUpdateAll           = 107;
    public const int ProjectDeleteAll           = 108;
    public const int ProjectGenerateTasks       = 109;
    public const int ProjectCompleteDepartment  = 110;

    public const int TaskReadAll                = 111;
    public const int TaskReadDepartment         = 112;
    public const int TaskReadAssigned           = 113;
    public const int TaskReadConfidential       = 114;

    public const int TaskCreateDepartment          = 115;
    public const int TaskCreateConfidential        = 116;
    public const int TaskUpdateDepartment          = 117;
    public const int TaskUpdateProgressDepartment  = 118;
    public const int TaskCompleteDepartment        = 119;
    public const int TaskDeleteDepartment          = 120;

    public const int ProductionAttachmentReadDepartment  = 121;
    public const int ProductionAttachmentWriteDepartment = 122;
    public const int ProductionAttachmentReadAll         = 123;

    public const int TaskAuditReadAll           = 124;
    public const int TaskAuditReadDepartment    = 125;
}
