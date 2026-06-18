using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Enums.Compliance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.AllowanceExemptThreshold.Commands;

public sealed class CreateAllowanceExemptThresholdCommandHandler : IRequestHandler<CreateAllowanceExemptThresholdCommand, int>
{
    private readonly IAllowanceExemptThresholdRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public CreateAllowanceExemptThresholdCommandHandler(
        IAllowanceExemptThresholdRepository repo, ILegalDocumentRepository legalDocumentRepo, ICurrentUserService currentUser, IClock clock, IPublisher publisher)
    {
        _repo = repo; _legalDocumentRepo = legalDocumentRepo; _currentUser = currentUser; _clock = clock; _publisher = publisher;
    }

    public async Task<int> Handle(CreateAllowanceExemptThresholdCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var entity = new AllowanceExemptThresholdVersion
        {
            AllowanceTypeId = request.AllowanceTypeId,
            ThresholdMode = request.ThresholdMode,
            MonthlyExemptAmount = request.MonthlyExemptAmount,
            AppliesToTax = request.AppliesToTax,
            AppliesToSocial = request.AppliesToSocial,
            LegalReference = request.LegalReference ?? string.Empty,
            Note = request.Note,
            CreatedByUserId = userId,
            CreatedAt = _clock.UtcNow,
        };
        var id = await _repo.AddAsync(entity, applyImmediately: true, ct);

        foreach (var legalDocumentId in request.LegalDocumentIds)
        {
            await _legalDocumentRepo.LinkToVersionAsync(new ComplianceVersionLegalDocument
            {
                ComplianceEntityType = ComplianceEntityTypes.AllowanceExemptThreshold,
                ComplianceVersionId = id,
                LegalDocumentId = legalDocumentId,
                LinkedByUserId = userId,
                LinkedAt = _clock.UtcNow,
            }, ct);
        }

        await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(AllowanceExemptThresholdVersion)), ct);
        return id;
    }
}

public sealed class UpdateAllowanceExemptThresholdCommandHandler : IRequestHandler<UpdateAllowanceExemptThresholdCommand, bool>
{
    private readonly IAllowanceExemptThresholdRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public UpdateAllowanceExemptThresholdCommandHandler(
        IAllowanceExemptThresholdRepository repo, ILegalDocumentRepository legalDocumentRepo, ICurrentUserService currentUser, IClock clock, IPublisher publisher)
    {
        _repo = repo; _legalDocumentRepo = legalDocumentRepo; _currentUser = currentUser; _clock = clock; _publisher = publisher;
    }

    public async Task<bool> Handle(UpdateAllowanceExemptThresholdCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var entity = new AllowanceExemptThresholdVersion
        {
            Id = request.Id,
            ThresholdMode = request.ThresholdMode,
            MonthlyExemptAmount = request.MonthlyExemptAmount,
            AppliesToTax = request.AppliesToTax,
            AppliesToSocial = request.AppliesToSocial,
            LegalReference = request.LegalReference ?? string.Empty,
            Note = request.Note,
            UpdatedByUserId = userId,
        };
        var result = await _repo.UpdateAsync(entity, ct);
        if (!result) return false;

        await SyncLegalDocumentLinksAsync(request.Id, request.LegalDocumentIds, userId, ct);
        await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(AllowanceExemptThresholdVersion)), ct);
        return result;
    }

    private async Task SyncLegalDocumentLinksAsync(int versionId, IReadOnlyList<int> desiredIds, int userId, CancellationToken ct)
    {
        var existing = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.AllowanceExemptThreshold, versionId, ct);
        var existingIds = existing.Select(d => d.Id).ToHashSet();
        var desiredSet = desiredIds.ToHashSet();

        foreach (var toAdd in desiredSet.Except(existingIds))
        {
            await _legalDocumentRepo.LinkToVersionAsync(new ComplianceVersionLegalDocument
            {
                ComplianceEntityType = ComplianceEntityTypes.AllowanceExemptThreshold,
                ComplianceVersionId = versionId,
                LegalDocumentId = toAdd,
                LinkedByUserId = userId,
                LinkedAt = _clock.UtcNow,
            }, ct);
        }

        foreach (var toRemove in existingIds.Except(desiredSet))
        {
            await _legalDocumentRepo.UnlinkFromVersionAsync(ComplianceEntityTypes.AllowanceExemptThreshold, versionId, toRemove, ct);
        }
    }
}

public sealed class DeleteAllowanceExemptThresholdPendingCommandHandler : IRequestHandler<DeleteAllowanceExemptThresholdPendingCommand, bool>
{
    private readonly IAllowanceExemptThresholdRepository _repo;
    private readonly IPublisher _publisher;

    public DeleteAllowanceExemptThresholdPendingCommandHandler(IAllowanceExemptThresholdRepository repo, IPublisher publisher) { _repo = repo; _publisher = publisher; }

    public async Task<bool> Handle(DeleteAllowanceExemptThresholdPendingCommand request, CancellationToken ct)
    {
        var result = await _repo.DeletePendingAsync(request.Id, ct);
        if (result) await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(AllowanceExemptThresholdVersion)), ct);
        return result;
    }
}
