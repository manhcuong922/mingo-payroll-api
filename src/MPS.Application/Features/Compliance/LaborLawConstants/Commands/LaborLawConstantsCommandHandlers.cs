using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.LaborLawConstants.Commands;

public sealed class CreateLaborLawConstantsCommandHandler : IRequestHandler<CreateLaborLawConstantsCommand, int>
{
    private readonly ILaborLawConstantsRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public CreateLaborLawConstantsCommandHandler(ILaborLawConstantsRepository repo, ICurrentUserService currentUser, IClock clock, IPublisher publisher)
    {
        _repo = repo; _currentUser = currentUser; _clock = clock; _publisher = publisher;
    }

    public async Task<int> Handle(CreateLaborLawConstantsCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var entity = new LaborLawConstantsVersion
        {
            ValidFrom = request.ValidFrom,
            ProbationSalaryMinPercent = request.ProbationSalaryMinPercent,
            NightShiftSurchargePercent = request.NightShiftSurchargePercent,
            StandardWorkingHoursPerDay = request.StandardWorkingHoursPerDay,
            StandardWorkingDaysPerMonth = request.StandardWorkingDaysPerMonth,
            AnnualLeaveMinDays = request.AnnualLeaveMinDays,
            OvertimeMaxHoursPerDay = request.OvertimeMaxHoursPerDay,
            OvertimeMaxHoursPerYear = request.OvertimeMaxHoursPerYear,
            LegalReference = request.LegalReference,
            Note = request.Note,
            CreatedByUserId = userId,
            CreatedAt = _clock.UtcNow,
        };
        var id = await _repo.AddAsync(entity, request.ApplyImmediately, ct);
        await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(LaborLawConstantsVersion)), ct);
        return id;
    }
}

public sealed class UpdateLaborLawConstantsCommandHandler : IRequestHandler<UpdateLaborLawConstantsCommand, bool>
{
    private readonly ILaborLawConstantsRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public UpdateLaborLawConstantsCommandHandler(ILaborLawConstantsRepository repo, ICurrentUserService currentUser, IClock clock, IPublisher publisher)
    {
        _repo = repo; _currentUser = currentUser; _clock = clock; _publisher = publisher;
    }

    public async Task<bool> Handle(UpdateLaborLawConstantsCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var entity = new LaborLawConstantsVersion
        {
            Id = request.Id,
            ValidFrom = request.ValidFrom,
            ProbationSalaryMinPercent = request.ProbationSalaryMinPercent,
            NightShiftSurchargePercent = request.NightShiftSurchargePercent,
            StandardWorkingHoursPerDay = request.StandardWorkingHoursPerDay,
            StandardWorkingDaysPerMonth = request.StandardWorkingDaysPerMonth,
            AnnualLeaveMinDays = request.AnnualLeaveMinDays,
            OvertimeMaxHoursPerDay = request.OvertimeMaxHoursPerDay,
            OvertimeMaxHoursPerYear = request.OvertimeMaxHoursPerYear,
            LegalReference = request.LegalReference,
            Note = request.Note,
            UpdatedByUserId = userId,
        };
        var result = await _repo.UpdateAsync(entity, ct);
        if (result) await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(LaborLawConstantsVersion)), ct);
        return result;
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
