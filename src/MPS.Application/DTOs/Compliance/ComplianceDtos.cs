namespace MPS.Application.DTOs.Compliance;

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
    int UnemploymentCapMultiplier,
    string LegalReference,
    string? Note,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record PitBracketDto(int Order, decimal LowerBound, decimal? UpperBound, decimal Rate);

public sealed record PitRegulationDto(
    int Id,
    DateOnly ValidFrom,
    bool IsActive,
    bool IsArchived,
    decimal PersonalDeductionAmount,
    decimal DependentDeductionAmount,
    IReadOnlyList<PitBracketDto> Brackets,
    string LegalReference,
    string? Note,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record OtRateEntryDto(string OtType, string Label, decimal Rate);

public sealed record OtRateDto(
    int Id,
    DateOnly ValidFrom,
    bool IsActive,
    bool IsArchived,
    IReadOnlyList<OtRateEntryDto> Entries,
    string LegalReference,
    string? Note,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record MinimumWageRegionDto(string RegionCode, decimal AmountPerMonth);

public sealed record MinimumWageDto(
    int Id,
    DateOnly ValidFrom,
    bool IsActive,
    bool IsArchived,
    IReadOnlyList<MinimumWageRegionDto> Regions,
    string LegalReference,
    string? Note,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

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
    DateTime CreatedAt,
    DateTime? UpdatedAt);

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
    DateTime CreatedAt,
    DateTime? UpdatedAt);
