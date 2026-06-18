namespace MPS.Application.DTOs.Compliance;

public sealed record AuditUserDto(int UserId, string FullName, string Email);

public sealed record SocialInsuranceFundEntryDto(
    string FundCode,
    string FundName,
    decimal EmployeePercent,
    decimal EmployerPercent);

public sealed record SocialInsuranceRateDto(
    int Id,
    DateOnly ValidFrom,
    bool IsActive,
    bool IsArchived,
    decimal EmployeeSocialPercent,
    decimal EmployerSocialPercent,
    decimal EmployerOccupationalDiseasePercent,
    decimal EmployeeHealthPercent,
    decimal EmployerHealthPercent,
    decimal EmployeeUnemploymentPercent,
    decimal EmployerUnemploymentPercent,
    decimal EmployerUnionFundPercent,
    decimal ReferenceAmount,
    int SocialCapMultiplier,
    int HealthCapMultiplier,
    int UnemploymentCapMultiplier,
    IReadOnlyList<SocialInsuranceFundEntryDto> Funds,
    string EligibilityMode,
    IReadOnlyList<string> EligibleContractSubtypes,
    int? MinTenureMonths,
    string LegalReference,
    string? Note,
    AuditUserDto CreatedBy,
    DateTime CreatedAt,
    AuditUserDto? UpdatedBy,
    DateTime? UpdatedAt,
    IReadOnlyList<LegalDocumentSummaryDto> LegalDocuments);

public sealed record LegalDocumentSummaryDto(
    int Id,
    string Code,
    string DocumentNumber,
    string Title,
    DateOnly EffectiveDate,
    bool IsActive);

public sealed record LegalDocumentDto(
    int Id,
    string Code,
    string DocumentType,
    string DocumentNumber,
    string Title,
    string IssuedBy,
    DateOnly IssuedDate,
    DateOnly EffectiveDate,
    DateOnly? ExpiryDate,
    string? Url,
    string? Note,
    bool IsActive,
    AuditUserDto CreatedBy,
    DateTime CreatedAt,
    AuditUserDto? UpdatedBy,
    DateTime? UpdatedAt);

public sealed record ComplianceVersionWithDocDto(string ComplianceEntityType, int ComplianceVersionId);

public sealed record PitBracketDto(int Order, decimal LowerBound, decimal? UpperBound, decimal Rate);

public sealed record PitRegulationDto(
    int Id,
    DateOnly ValidFrom,
    bool IsActive,
    bool IsArchived,
    decimal PersonalDeductionAmount,
    decimal DependentDeductionAmount,
    int ShortTermContractMaxMonths,
    decimal ShortTermFlatRatePercent,
    decimal ShortTermTaxExemptMonthlyIncome,
    IReadOnlyList<PitBracketDto> Brackets,
    string LegalReference,
    string? Note,
    AuditUserDto CreatedBy,
    DateTime CreatedAt,
    AuditUserDto? UpdatedBy,
    DateTime? UpdatedAt,
    IReadOnlyList<LegalDocumentSummaryDto> LegalDocuments);

public sealed record MinimumWageRegionDto(string RegionCode, decimal AmountPerMonth);

public sealed record MinimumWageDto(
    int Id,
    DateOnly ValidFrom,
    bool IsActive,
    bool IsArchived,
    string AppliedRegionCode,
    IReadOnlyList<MinimumWageRegionDto> Regions,
    string LegalReference,
    string? Note,
    AuditUserDto CreatedBy,
    DateTime CreatedAt,
    AuditUserDto? UpdatedBy,
    DateTime? UpdatedAt,
    IReadOnlyList<LegalDocumentSummaryDto> LegalDocuments);

public sealed record AllowanceExemptThresholdDto(
    int Id,
    DateOnly ValidFrom,
    bool IsActive,
    bool IsArchived,
    int AllowanceTypeId,
    string AllowanceTypeCode,
    string AllowanceTypeName,
    string ThresholdMode,
    decimal? MonthlyExemptAmount,
    bool AppliesToTax,
    bool AppliesToSocial,
    string LegalReference,
    string? Note,
    AuditUserDto CreatedBy,
    DateTime CreatedAt,
    AuditUserDto? UpdatedBy,
    DateTime? UpdatedAt,
    IReadOnlyList<LegalDocumentSummaryDto> LegalDocuments);

public sealed record LaborLawConstantsDto(
    int Id,
    DateOnly ValidFrom,
    bool IsActive,
    bool IsArchived,
    decimal ProbationSalaryMinPercent,
    decimal NightShiftSurchargePercent,
    decimal StandardWorkingHoursPerDay,
    decimal StandardWorkingDaysPerMonth,
    int AnnualLeaveMinDays,
    decimal OvertimeMaxHoursPerDay,
    int OvertimeMaxHoursPerYear,
    string LegalReference,
    string? Note,
    AuditUserDto CreatedBy,
    DateTime CreatedAt,
    AuditUserDto? UpdatedBy,
    DateTime? UpdatedAt,
    IReadOnlyList<LegalDocumentSummaryDto> LegalDocuments);
