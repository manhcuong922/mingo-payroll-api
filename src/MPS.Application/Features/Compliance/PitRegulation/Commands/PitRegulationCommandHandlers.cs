using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.PitRegulation.Commands;

public sealed class CreatePitRegulationCommandHandler : IRequestHandler<CreatePitRegulationCommand, int>
{
    private readonly IPitRegulationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public CreatePitRegulationCommandHandler(IPitRegulationRepository repo, ICurrentUserService currentUser, IClock clock, IPublisher publisher)
    {
        _repo = repo; _currentUser = currentUser; _clock = clock; _publisher = publisher;
    }

    public async Task<int> Handle(CreatePitRegulationCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var entity = new PitRegulationVersion
        {
            ValidFrom = request.ValidFrom,
            PersonalDeductionAmount = request.PersonalDeductionAmount,
            DependentDeductionAmount = request.DependentDeductionAmount,
            LegalReference = request.LegalReference,
            Note = request.Note,
            CreatedByUserId = userId,
            CreatedAt = _clock.UtcNow,
            Brackets = request.Brackets.Select(b => new PitBracket
            {
                Order = b.Order, LowerBound = b.LowerBound, UpperBound = b.UpperBound, Rate = b.Rate
            }).ToList()
        };
        var id = await _repo.AddAsync(entity, request.ApplyImmediately, ct);
        await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(PitRegulationVersion)), ct);
        return id;
    }
}

public sealed class UpdatePitRegulationCommandHandler : IRequestHandler<UpdatePitRegulationCommand, bool>
{
    private readonly IPitRegulationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public UpdatePitRegulationCommandHandler(IPitRegulationRepository repo, ICurrentUserService currentUser, IClock clock, IPublisher publisher)
    {
        _repo = repo; _currentUser = currentUser; _clock = clock; _publisher = publisher;
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
            LegalReference = request.LegalReference,
            Note = request.Note,
            UpdatedByUserId = userId,
            Brackets = request.Brackets.Select(b => new PitBracket
            {
                Order = b.Order, LowerBound = b.LowerBound, UpperBound = b.UpperBound, Rate = b.Rate
            }).ToList()
        };
        var result = await _repo.UpdateAsync(entity, ct);
        if (result) await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(PitRegulationVersion)), ct);
        return result;
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
