using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Enums.Compliance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Commands;

public sealed class UpdateSocialInsuranceRateCommandHandler : IRequestHandler<UpdateSocialInsuranceRateCommand, bool>
{
    private readonly ISocialInsuranceRateRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public UpdateSocialInsuranceRateCommandHandler(
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

    public async Task<bool> Handle(UpdateSocialInsuranceRateCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

#pragma warning disable CS0618 // Obsolete — still settable for backward-compat columns
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
            HealthCapMultiplier = request.HealthCapMultiplier,
            UnemploymentCapMultiplier = request.UnemploymentCapMultiplier,
            EligibilityMode = request.EligibilityMode,
            EligibleContractSubtypesCsv = request.EligibleContractSubtypes is { Count: > 0 }
                ? string.Join(",", request.EligibleContractSubtypes)
                : null,
            MinTenureMonths = request.MinTenureMonths,
            LegalReference = request.LegalReference ?? string.Empty,
            Note = request.Note,
            UpdatedByUserId = userId,
            Funds = request.Funds.Select(f => new SocialInsuranceFundEntry
            {
                FundCode = f.FundCode,
                FundName = f.FundName,
                EmployeePercent = f.EmployeePercent,
                EmployerPercent = f.EmployerPercent,
            }).ToList(),
        };
#pragma warning restore CS0618

        var result = await _repo.UpdateAsync(entity, cancellationToken);
        if (!result) return false;

        await SyncLegalDocumentLinksAsync(request.Id, request.LegalDocumentIds, userId, cancellationToken);
        await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(SocialInsuranceRateVersion)), cancellationToken);
        return result;
    }

    private async Task SyncLegalDocumentLinksAsync(
        int versionId, IReadOnlyList<int> desiredIds, int userId, CancellationToken ct)
    {
        var existing = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.SocialInsuranceRate, versionId, ct);
        var existingIds = existing.Select(d => d.Id).ToHashSet();
        var desiredSet = desiredIds.ToHashSet();

        foreach (var toAdd in desiredSet.Except(existingIds))
        {
            await _legalDocumentRepo.LinkToVersionAsync(new ComplianceVersionLegalDocument
            {
                ComplianceEntityType = ComplianceEntityTypes.SocialInsuranceRate,
                ComplianceVersionId = versionId,
                LegalDocumentId = toAdd,
                LinkedByUserId = userId,
                LinkedAt = _clock.UtcNow,
            }, ct);
        }

        foreach (var toRemove in existingIds.Except(desiredSet))
        {
            await _legalDocumentRepo.UnlinkFromVersionAsync(ComplianceEntityTypes.SocialInsuranceRate, versionId, toRemove, ct);
        }
    }
}
