using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
