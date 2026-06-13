using MediatR;
using MPS.Application.DTOs.Compliance;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Queries;

public sealed record GetSocialInsuranceRateByIdQuery(int Id) : IRequest<SocialInsuranceRateDto?>;
