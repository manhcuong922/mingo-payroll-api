using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Entities.Compliance.AutoUpdate;

namespace MPS.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<PerformanceMetric> PerformanceMetrics => Set<PerformanceMetric>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<TypePerformanceMetric> TypePerformanceMetrics => Set<TypePerformanceMetric>();
    public DbSet<User> Users => Set<User>();
    public DbSet<EmployeeInformation> EmployeeInformations => Set<EmployeeInformation>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    // Contract module
    public DbSet<ContractParty> ContractParties => Set<ContractParty>();
    public DbSet<ContractTemplate> ContractTemplates => Set<ContractTemplate>();
    public DbSet<ContractTerminationReasonRef> ContractTerminationReasonRefs => Set<ContractTerminationReasonRef>();
    public DbSet<SalaryGrade> SalaryGrades => Set<SalaryGrade>();
    public DbSet<AllowanceType> AllowanceTypes => Set<AllowanceType>();
    public DbSet<ContractFile> ContractFiles => Set<ContractFile>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractAllowance> ContractAllowances => Set<ContractAllowance>();
    public DbSet<ContractAppendix> ContractAppendices => Set<ContractAppendix>();
    public DbSet<ContractTermination> ContractTerminations => Set<ContractTermination>();

    // Audit log module (FT-39)
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AuditLogArchive> AuditLogsArchive => Set<AuditLogArchive>();

    // Company Policy module
    public DbSet<CompanyPolicy> CompanyPolicies => Set<CompanyPolicy>();
    public DbSet<CompanyPolicyFile> CompanyPolicyFiles => Set<CompanyPolicyFile>();

    // Compliance module (FE-06)
    public DbSet<SocialInsuranceRateVersion> SocialInsuranceRateVersions => Set<SocialInsuranceRateVersion>();
    public DbSet<SocialInsuranceFundEntry> SocialInsuranceFundEntries => Set<SocialInsuranceFundEntry>();
    public DbSet<PitRegulationVersion> PitRegulationVersions => Set<PitRegulationVersion>();
    public DbSet<PitBracket> PitBrackets => Set<PitBracket>();
    public DbSet<MinimumWageVersion> MinimumWageVersions => Set<MinimumWageVersion>();
    public DbSet<MinimumWageRegion> MinimumWageRegions => Set<MinimumWageRegion>();
    public DbSet<AllowanceExemptThresholdVersion> AllowanceExemptThresholdVersions => Set<AllowanceExemptThresholdVersion>();
    public DbSet<LaborLawConstantsVersion> LaborLawConstantsVersions => Set<LaborLawConstantsVersion>();
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
    public DbSet<ComplianceVersionLegalDocument> ComplianceVersionLegalDocuments => Set<ComplianceVersionLegalDocument>();

    // Compliance Auto-Update module (crawl + AI đề xuất cập nhật pháp luật)
    public DbSet<SourceSnapshot> ComplianceSourceSnapshots => Set<SourceSnapshot>();
    public DbSet<CrawledDocumentLog> ComplianceCrawledDocumentLogs => Set<CrawledDocumentLog>();
    public DbSet<ComplianceUpdateProposal> ComplianceUpdateProposals => Set<ComplianceUpdateProposal>();
    public DbSet<ComplianceAuthoritativeSource> ComplianceAuthoritativeSources => Set<ComplianceAuthoritativeSource>();
    public DbSet<ComplianceLead> ComplianceLeads => Set<ComplianceLead>();

    // Attendance module (FE-ATT)
    public DbSet<FaceRegistration> FaceRegistrations => Set<FaceRegistration>();
    public DbSet<ShiftTemplate> ShiftTemplates => Set<ShiftTemplate>();
    public DbSet<EmployeeShiftAssignment> EmployeeShiftAssignments => Set<EmployeeShiftAssignment>();
    public DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AttendanceSetting> AttendanceSettings => Set<AttendanceSetting>();
    public DbSet<OvertimeRequest> OvertimeRequests => Set<OvertimeRequest>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<AttendanceAdjustment> AttendanceAdjustments => Set<AttendanceAdjustment>();
    public DbSet<AttendanceRule> AttendanceRules => Set<AttendanceRule>();
    public DbSet<ScheduleTemplate> ScheduleTemplates => Set<ScheduleTemplate>();
    public DbSet<ScheduleTemplateEmployee> ScheduleTemplateEmployees => Set<ScheduleTemplateEmployee>();
    public DbSet<ScheduleTemplateShift> ScheduleTemplateShifts => Set<ScheduleTemplateShift>();
    // ===== Task/Production module =====
    public DbSet<ProcessTemplate> ProcessTemplates => Set<ProcessTemplate>();
    public DbSet<ProcessStep> ProcessSteps => Set<ProcessStep>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<TaskAssignee> TaskAssignees => Set<TaskAssignee>();
    public DbSet<ProductionAttachment> ProductionAttachments => Set<ProductionAttachment>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
