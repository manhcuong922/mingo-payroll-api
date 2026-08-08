using MediatR;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Commands;

public sealed record UpdateSocialInsuranceRateCommand(
    int Id,
    DateOnly ValidFrom,
    decimal EmployeeSocialPercent,
    decimal EmployerSocialPercent,
    decimal EmployerOccupationalDiseasePercent,
    decimal EmployeeHealthPercent,
    decimal EmployerHealthPercent,
    decimal EmployeeUnemploymentPercent,
    decimal EmployerUnemploymentPercent,
    decimal EmployerUnionFundPercent,
    decimal EmployeeUnionDuesPercent,
    decimal EmployeeUnionDuesCapPercent,
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
    IReadOnlyList<int> LegalDocumentIds) : IRequest<bool>;
