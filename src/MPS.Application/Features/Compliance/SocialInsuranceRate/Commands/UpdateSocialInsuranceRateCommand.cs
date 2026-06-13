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
    decimal ReferenceAmount,
    int SocialCapMultiplier,
    int UnemploymentCapMultiplier,
    string LegalReference,
    string? Note) : IRequest<bool>;
