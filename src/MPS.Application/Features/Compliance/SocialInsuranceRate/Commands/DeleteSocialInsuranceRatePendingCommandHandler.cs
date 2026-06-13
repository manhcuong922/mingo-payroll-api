using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Commands;

public sealed class DeleteSocialInsuranceRatePendingCommandHandler : IRequestHandler<DeleteSocialInsuranceRatePendingCommand, bool>
{
    private readonly ISocialInsuranceRateRepository _repo;
    private readonly IPublisher _publisher;

    public DeleteSocialInsuranceRatePendingCommandHandler(ISocialInsuranceRateRepository repo, IPublisher publisher)
    {
        _repo = repo;
        _publisher = publisher;
    }

    public async Task<bool> Handle(DeleteSocialInsuranceRatePendingCommand request, CancellationToken cancellationToken)
    {
        var result = await _repo.DeletePendingAsync(request.Id, cancellationToken);
        if (result)
            await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(SocialInsuranceRateVersion)), cancellationToken);
        return result;
    }
}
