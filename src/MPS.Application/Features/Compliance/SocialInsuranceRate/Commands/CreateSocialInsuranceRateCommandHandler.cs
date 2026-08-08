using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Enums.Compliance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Commands;

public sealed class CreateSocialInsuranceRateCommandHandler : IRequestHandler<CreateSocialInsuranceRateCommand, int>
{
    private readonly ISocialInsuranceRateRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public CreateSocialInsuranceRateCommandHandler(
        ISocialInsuranceRateRepository repo,
        ILegalDocumentRepository legalDocumentRepo,
        ICurrentUserService currentUser,
        IClock clock,
        IPublisher publisher)
    {
        _repo = repo;
        _legalDocumentRepo = legalDocumentRepo;
        _currentUser = currentUser;
        _clock = clock;
        _publisher = publisher;
    }

    public async Task<int> Handle(CreateSocialInsuranceRateCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

#pragma warning disable CS0618 // Obsolete — still settable for backward-compat columns
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
            EmployeeUnionDuesPercent = request.EmployeeUnionDuesPercent,
            EmployeeUnionDuesCapPercent = request.EmployeeUnionDuesCapPercent,
            ReferenceAmount = request.ReferenceAmount,
            SocialCapMultiplier = request.SocialCapMultiplier,
            HealthCapMultiplier = request.HealthCapMultiplier,
            UnemploymentCapMultiplier = request.UnemploymentCapMultiplier,
            EligibilityMode = request.EligibilityMode,
            EligibleContractSubtypesCsv = request.EligibleContractSubtypes is { Count: > 0 }
                ? string.Join(",", request.EligibleContractSubtypes)
                : null,
            MinTenureMonths = request.MinTenureMonths,
            LegalReference = request.LegalReference ?? string.Empty,
            Note = request.Note,
            CreatedByUserId = userId,
            CreatedAt = _clock.UtcNow,
            Funds = request.Funds.Select(f => new SocialInsuranceFundEntry
            {
                FundCode = f.FundCode,
                FundName = f.FundName,
                EmployeePercent = f.EmployeePercent,
                EmployerPercent = f.EmployerPercent,
            }).ToList(),
        };
#pragma warning restore CS0618

        var id = await _repo.AddAsync(entity, request.ApplyImmediately, cancellationToken);

        foreach (var legalDocumentId in request.LegalDocumentIds)
        {
            await _legalDocumentRepo.LinkToVersionAsync(new Domain.Entities.Compliance.ComplianceVersionLegalDocument
            {
                ComplianceEntityType = ComplianceEntityTypes.SocialInsuranceRate,
                ComplianceVersionId = id,
                LegalDocumentId = legalDocumentId,
                LinkedByUserId = userId,
                LinkedAt = _clock.UtcNow,
            }, cancellationToken);
        }

        await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(SocialInsuranceRateVersion)), cancellationToken);
        return id;
    }
}
