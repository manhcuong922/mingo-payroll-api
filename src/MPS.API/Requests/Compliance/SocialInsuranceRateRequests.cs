namespace MPS.API.Requests.Compliance;

public sealed record FundEntryInputRequest(string FundCode, string FundName, decimal EmployeePercent, decimal EmployerPercent);

public sealed record CreateSocialInsuranceRateRequest(
    DateOnly ValidFrom,
    bool ApplyImmediately,
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
    IReadOnlyList<FundEntryInputRequest> Funds,
    string EligibilityMode,
    IReadOnlyList<string>? EligibleContractSubtypes,
    int? MinTenureMonths,
    string? LegalReference,
    string? Note,
    IReadOnlyList<int> LegalDocumentIds);

public sealed record UpdateSocialInsuranceRateRequest(
    DateOnly ValidFrom,
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
    IReadOnlyList<FundEntryInputRequest> Funds,
    string EligibilityMode,
    IReadOnlyList<string>? EligibleContractSubtypes,
    int? MinTenureMonths,
    string? LegalReference,
    string? Note,
    IReadOnlyList<int> LegalDocumentIds);
