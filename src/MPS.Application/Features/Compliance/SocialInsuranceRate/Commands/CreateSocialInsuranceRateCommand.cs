using MediatR;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Commands;

public sealed record FundEntryInput(string FundCode, string FundName, decimal EmployeePercent, decimal EmployerPercent);

public sealed record CreateSocialInsuranceRateCommand(
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
    IReadOnlyList<FundEntryInput> Funds,
    string EligibilityMode,
    IReadOnlyList<string>? EligibleContractSubtypes,
    int? MinTenureMonths,
    string? LegalReference,
    string? Note,
    IReadOnlyList<int> LegalDocumentIds) : IRequest<int>;
