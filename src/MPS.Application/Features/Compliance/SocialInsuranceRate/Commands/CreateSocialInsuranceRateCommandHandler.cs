using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Commands;

public sealed class CreateSocialInsuranceRateCommandHandler : IRequestHandler<CreateSocialInsuranceRateCommand, int>
{
    private readonly ISocialInsuranceRateRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public CreateSocialInsuranceRateCommandHandler(
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

    public async Task<int> Handle(CreateSocialInsuranceRateCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var entity = new SocialInsuranceRateVersion
        {
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
            CreatedByUserId = userId,
            CreatedAt = _clock.UtcNow,
        };

        var id = await _repo.AddAsync(entity, request.ApplyImmediately, cancellationToken);
        await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(SocialInsuranceRateVersion)), cancellationToken);
        return id;
    }
}
