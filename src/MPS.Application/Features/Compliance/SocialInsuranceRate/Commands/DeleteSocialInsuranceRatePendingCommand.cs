using MediatR;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Commands;

public sealed record DeleteSocialInsuranceRatePendingCommand(int Id) : IRequest<bool>;
