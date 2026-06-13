using MediatR;
using MPS.Application.DTOs.Compliance;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Queries;

public sealed record GetAllSocialInsuranceRatesQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<SocialInsuranceRateDto>>;
