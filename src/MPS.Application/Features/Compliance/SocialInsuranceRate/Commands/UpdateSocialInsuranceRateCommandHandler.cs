using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Commands;

public sealed class UpdateSocialInsuranceRateCommandHandler : IRequestHandler<UpdateSocialInsuranceRateCommand, bool>
{
    private readonly ISocialInsuranceRateRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public UpdateSocialInsuranceRateCommandHandler(
        ISocialInsuranceRateRepository repo,
        ICurrentUserService currentUser,
        IClock clock,
        IPublisher publisher)
    {
        _repo = repo;
        _currentUser = currentUser;
        _clock = clock;
        _publisher = publisher;
    }

    public async Task<bool> Handle(UpdateSocialInsuranceRateCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var entity = new SocialInsuranceRateVersion
        {
            Id = request.Id,
            ValidFrom = request.ValidFrom,
            EmployeeSocialPercent = request.EmployeeSocialPercent,
            EmployerSocialPercent = request.EmployerSocialPercent,
            EmployerOccupationalDiseasePercent = request.EmployerOccupationalDiseasePercent,
            EmployeeHealthPercent = request.EmployeeHealthPercent,
            EmployerHealthPercent = request.EmployerHealthPercent,
            EmployeeUnemploymentPercent = request.EmployeeUnemploymentPercent,
            EmployerUnemploymentPercent = request.EmployerUnemploymentPercent,
            EmployerUnionFundPercent = request.EmployerUnionFundPercent,
            ReferenceAmount = request.ReferenceAmount,
            SocialCapMultiplier = request.SocialCapMultiplier,
            UnemploymentCapMultiplier = request.UnemploymentCapMultiplier,
            LegalReference = request.LegalReference,
            Note = request.Note,
            UpdatedByUserId = userId,
        };

        var result = await _repo.UpdateAsync(entity, cancellationToken);
        if (result)
            await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(SocialInsuranceRateVersion)), cancellationToken);
        return result;
    }
}
