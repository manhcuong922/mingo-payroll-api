using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Enums.Compliance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.LaborLawConstants.Commands;

public sealed class CreateLaborLawConstantsCommandHandler : IRequestHandler<CreateLaborLawConstantsCommand, int>
{
    private readonly ILaborLawConstantsRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public CreateLaborLawConstantsCommandHandler(
        ILaborLawConstantsRepository repo, ILegalDocumentRepository legalDocumentRepo, ICurrentUserService currentUser, IClock clock, IPublisher publisher)
    {
        _repo = repo; _legalDocumentRepo = legalDocumentRepo; _currentUser = currentUser; _clock = clock; _publisher = publisher;
    }

    public async Task<int> Handle(CreateLaborLawConstantsCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var entity = new LaborLawConstantsVersion
        {
            ValidFrom = request.ValidFrom,
            ProbationSalaryMinPercent = request.ProbationSalaryMinPercent,
            OvertimeNightExtraPercent = request.OvertimeNightExtraPercent,
            NightWorkSurchargePercent = request.NightWorkSurchargePercent,
            OvertimeNormalUpliftPercent = request.OvertimeNormalUpliftPercent,
            NightStartTime = request.NightStartTime,
            NightEndTime = request.NightEndTime,
            StandardWorkingHoursPerDay = request.StandardWorkingHoursPerDay,
            MaxRegularHoursPerWeek = request.MaxRegularHoursPerWeek,
            StandardWorkingDaysPerMonth = request.StandardWorkingDaysPerMonth,
            AnnualLeaveMinDays = request.AnnualLeaveMinDays,
            WorkRateNormalFloor = request.WorkRateNormalFloor,
            WorkRateWeeklyRestFloor = request.WorkRateWeeklyRestFloor,
            WorkRateHolidayFloor = request.WorkRateHolidayFloor,
            OvertimeMaxHoursPerDay = request.OvertimeMaxHoursPerDay,
            OvertimeMaxHoursPerMonth = request.OvertimeMaxHoursPerMonth,
            OvertimeMaxHoursPerYear = request.OvertimeMaxHoursPerYear,
            LegalReference = request.LegalReference ?? string.Empty,
            Note = request.Note,
            CreatedByUserId = userId,
            CreatedAt = _clock.UtcNow,
        };
        var id = await _repo.AddAsync(entity, request.ApplyImmediately, ct);

        foreach (var legalDocumentId in request.LegalDocumentIds)
        {
            await _legalDocumentRepo.LinkToVersionAsync(new ComplianceVersionLegalDocument
            {
                ComplianceEntityType = ComplianceEntityTypes.LaborLawConstants,
                ComplianceVersionId = id,
                LegalDocumentId = legalDocumentId,
                LinkedByUserId = userId,
                LinkedAt = _clock.UtcNow,
            }, ct);
        }

        await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(LaborLawConstantsVersion)), ct);
        return id;
    }
}

public sealed class UpdateLaborLawConstantsCommandHandler : IRequestHandler<UpdateLaborLawConstantsCommand, bool>
{
    private readonly ILaborLawConstantsRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public UpdateLaborLawConstantsCommandHandler(
        ILaborLawConstantsRepository repo, ILegalDocumentRepository legalDocumentRepo, ICurrentUserService currentUser, IClock clock, IPublisher publisher)
    {
        _repo = repo; _legalDocumentRepo = legalDocumentRepo; _currentUser = currentUser; _clock = clock; _publisher = publisher;
    }

    public async Task<bool> Handle(UpdateLaborLawConstantsCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var entity = new LaborLawConstantsVersion
        {
            Id = request.Id,
            ValidFrom = request.ValidFrom,
            ProbationSalaryMinPercent = request.ProbationSalaryMinPercent,
            OvertimeNightExtraPercent = request.OvertimeNightExtraPercent,
            NightWorkSurchargePercent = request.NightWorkSurchargePercent,
            OvertimeNormalUpliftPercent = request.OvertimeNormalUpliftPercent,
            NightStartTime = request.NightStartTime,
            NightEndTime = request.NightEndTime,
            StandardWorkingHoursPerDay = request.StandardWorkingHoursPerDay,
            MaxRegularHoursPerWeek = request.MaxRegularHoursPerWeek,
            StandardWorkingDaysPerMonth = request.StandardWorkingDaysPerMonth,
            AnnualLeaveMinDays = request.AnnualLeaveMinDays,
            WorkRateNormalFloor = request.WorkRateNormalFloor,
            WorkRateWeeklyRestFloor = request.WorkRateWeeklyRestFloor,
            WorkRateHolidayFloor = request.WorkRateHolidayFloor,
            OvertimeMaxHoursPerDay = request.OvertimeMaxHoursPerDay,
            OvertimeMaxHoursPerMonth = request.OvertimeMaxHoursPerMonth,
            OvertimeMaxHoursPerYear = request.OvertimeMaxHoursPerYear,
            LegalReference = request.LegalReference ?? string.Empty,
            Note = request.Note,
            UpdatedByUserId = userId,
        };
        var result = await _repo.UpdateAsync(entity, ct);
        if (!result) return false;

        await SyncLegalDocumentLinksAsync(request.Id, request.LegalDocumentIds, userId, ct);
        await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(LaborLawConstantsVersion)), ct);
        return result;
    }

    private async Task SyncLegalDocumentLinksAsync(int versionId, IReadOnlyList<int> desiredIds, int userId, CancellationToken ct)
    {
        var existing = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.LaborLawConstants, versionId, ct);
        var existingIds = existing.Select(d => d.Id).ToHashSet();
        var desiredSet = desiredIds.ToHashSet();

        foreach (var toAdd in desiredSet.Except(existingIds))
        {
            await _legalDocumentRepo.LinkToVersionAsync(new ComplianceVersionLegalDocument
            {
                ComplianceEntityType = ComplianceEntityTypes.LaborLawConstants,
                ComplianceVersionId = versionId,
                LegalDocumentId = toAdd,
                LinkedByUserId = userId,
                LinkedAt = _clock.UtcNow,
            }, ct);
        }

        foreach (var toRemove in existingIds.Except(desiredSet))
        {
            await _legalDocumentRepo.UnlinkFromVersionAsync(ComplianceEntityTypes.LaborLawConstants, versionId, toRemove, ct);
        }
    }
}

public sealed class DeleteLaborLawConstantsPendingCommandHandler : IRequestHandler<DeleteLaborLawConstantsPendingCommand, bool>
{
    private readonly ILaborLawConstantsRepository _repo;
    private readonly IPublisher _publisher;

    public DeleteLaborLawConstantsPendingCommandHandler(ILaborLawConstantsRepository repo, IPublisher publisher) { _repo = repo; _publisher = publisher; }

    public async Task<bool> Handle(DeleteLaborLawConstantsPendingCommand request, CancellationToken ct)
    {
        var result = await _repo.DeletePendingAsync(request.Id, ct);
        if (result) await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(LaborLawConstantsVersion)), ct);
        return result;
    }
}
