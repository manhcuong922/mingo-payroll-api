using MediatR;
using MPS.Application.DTOs.Compliance;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Queries;

public sealed record GetActiveSocialInsuranceRateQuery : IRequest<SocialInsuranceRateDto?>;

public sealed record GetActiveSocialInsuranceRateAtQuery(DateOnly Date) : IRequest<SocialInsuranceRateDto?>;
