using MediatR;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Commands;

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
    int UnemploymentCapMultiplier,
    string LegalReference,
    string? Note) : IRequest<int>;
