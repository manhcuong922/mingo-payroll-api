using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Enums.Compliance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.PitRegulation.Commands;

public sealed class CreatePitRegulationCommandHandler : IRequestHandler<CreatePitRegulationCommand, int>
{
    private readonly IPitRegulationRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public CreatePitRegulationCommandHandler(
        IPitRegulationRepository repo, ILegalDocumentRepository legalDocumentRepo, ICurrentUserService currentUser, IClock clock, IPublisher publisher)
    {
        _repo = repo; _legalDocumentRepo = legalDocumentRepo; _currentUser = currentUser; _clock = clock; _publisher = publisher;
    }

    public async Task<int> Handle(CreatePitRegulationCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var entity = new PitRegulationVersion
        {
            ValidFrom = request.ValidFrom,
            PersonalDeductionAmount = request.PersonalDeductionAmount,
            DependentDeductionAmount = request.DependentDeductionAmount,
            ShortTermContractMaxMonths = request.ShortTermContractMaxMonths,
            ShortTermFlatRatePercent = request.ShortTermFlatRatePercent,
            ShortTermTaxExemptMonthlyIncome = request.ShortTermTaxExemptMonthlyIncome,
            HousingExemptRatePercent = request.HousingExemptRatePercent,
            LegalReference = request.LegalReference ?? string.Empty,
            Note = request.Note,
            CreatedByUserId = userId,
            CreatedAt = _clock.UtcNow,
            Brackets = request.Brackets.Select(b => new PitBracket
            {
                Order = b.Order, LowerBound = b.LowerBound, UpperBound = b.UpperBound, Rate = b.Rate
            }).ToList()
        };
        var id = await _repo.AddAsync(entity, request.ApplyImmediately, ct);

        foreach (var legalDocumentId in request.LegalDocumentIds)
        {
            await _legalDocumentRepo.LinkToVersionAsync(new ComplianceVersionLegalDocument
            {
                ComplianceEntityType = ComplianceEntityTypes.PitRegulation,
                ComplianceVersionId = id,
                LegalDocumentId = legalDocumentId,
                LinkedByUserId = userId,
                LinkedAt = _clock.UtcNow,
            }, ct);
        }

        await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(PitRegulationVersion)), ct);
        return id;
    }
}

public sealed class UpdatePitRegulationCommandHandler : IRequestHandler<UpdatePitRegulationCommand, bool>
{
    private readonly IPitRegulationRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public UpdatePitRegulationCommandHandler(
        IPitRegulationRepository repo, ILegalDocumentRepository legalDocumentRepo, ICurrentUserService currentUser, IClock clock, IPublisher publisher)
    {
        _repo = repo; _legalDocumentRepo = legalDocumentRepo; _currentUser = currentUser; _clock = clock; _publisher = publisher;
    }

    public async Task<bool> Handle(UpdatePitRegulationCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var entity = new PitRegulationVersion
        {
            Id = request.Id,
            ValidFrom = request.ValidFrom,
            PersonalDeductionAmount = request.PersonalDeductionAmount,
            DependentDeductionAmount = request.DependentDeductionAmount,
            ShortTermContractMaxMonths = request.ShortTermContractMaxMonths,
            ShortTermFlatRatePercent = request.ShortTermFlatRatePercent,
            ShortTermTaxExemptMonthlyIncome = request.ShortTermTaxExemptMonthlyIncome,
            HousingExemptRatePercent = request.HousingExemptRatePercent,
            LegalReference = request.LegalReference ?? string.Empty,
            Note = request.Note,
            UpdatedByUserId = userId,
            Brackets = request.Brackets.Select(b => new PitBracket
            {
                Order = b.Order, LowerBound = b.LowerBound, UpperBound = b.UpperBound, Rate = b.Rate
            }).ToList()
        };
        var result = await _repo.UpdateAsync(entity, ct);
        if (!result) return false;

        await SyncLegalDocumentLinksAsync(request.Id, request.LegalDocumentIds, userId, ct);
        await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(PitRegulationVersion)), ct);
        return result;
    }

    private async Task SyncLegalDocumentLinksAsync(int versionId, IReadOnlyList<int> desiredIds, int userId, CancellationToken ct)
    {
        var existing = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.PitRegulation, versionId, ct);
        var existingIds = existing.Select(d => d.Id).ToHashSet();
        var desiredSet = desiredIds.ToHashSet();

        foreach (var toAdd in desiredSet.Except(existingIds))
        {
            await _legalDocumentRepo.LinkToVersionAsync(new ComplianceVersionLegalDocument
            {
                ComplianceEntityType = ComplianceEntityTypes.PitRegulation,
                ComplianceVersionId = versionId,
                LegalDocumentId = toAdd,
                LinkedByUserId = userId,
                LinkedAt = _clock.UtcNow,
            }, ct);
        }

        foreach (var toRemove in existingIds.Except(desiredSet))
        {
            await _legalDocumentRepo.UnlinkFromVersionAsync(ComplianceEntityTypes.PitRegulation, versionId, toRemove, ct);
        }
    }
}

public sealed class DeletePitRegulationPendingCommandHandler : IRequestHandler<DeletePitRegulationPendingCommand, bool>
{
    private readonly IPitRegulationRepository _repo;
    private readonly IPublisher _publisher;

    public DeletePitRegulationPendingCommandHandler(IPitRegulationRepository repo, IPublisher publisher)
    {
        _repo = repo; _publisher = publisher;
    }

    public async Task<bool> Handle(DeletePitRegulationPendingCommand request, CancellationToken ct)
    {
        var result = await _repo.DeletePendingAsync(request.Id, ct);
        if (result) await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(PitRegulationVersion)), ct);
        return result;
    }
}
