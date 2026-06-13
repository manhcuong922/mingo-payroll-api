using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.MinimumWage.Commands;

public sealed class CreateMinimumWageCommandHandler : IRequestHandler<CreateMinimumWageCommand, int>
{
    private readonly IMinimumWageRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public CreateMinimumWageCommandHandler(IMinimumWageRepository repo, ICurrentUserService currentUser, IClock clock, IPublisher publisher)
    {
        _repo = repo; _currentUser = currentUser; _clock = clock; _publisher = publisher;
    }

    public async Task<int> Handle(CreateMinimumWageCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var entity = new MinimumWageVersion
        {
            ValidFrom = request.ValidFrom,
            LegalReference = request.LegalReference,
            Note = request.Note,
            CreatedByUserId = userId,
            CreatedAt = _clock.UtcNow,
            Regions = request.Regions.Select(r => new MinimumWageRegion { RegionCode = r.RegionCode, AmountPerMonth = r.AmountPerMonth }).ToList()
        };
        var id = await _repo.AddAsync(entity, request.ApplyImmediately, ct);
        await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(MinimumWageVersion)), ct);
        return id;
    }
}

public sealed class UpdateMinimumWageCommandHandler : IRequestHandler<UpdateMinimumWageCommand, bool>
{
    private readonly IMinimumWageRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public UpdateMinimumWageCommandHandler(IMinimumWageRepository repo, ICurrentUserService currentUser, IClock clock, IPublisher publisher)
    {
        _repo = repo; _currentUser = currentUser; _clock = clock; _publisher = publisher;
    }

    public async Task<bool> Handle(UpdateMinimumWageCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var entity = new MinimumWageVersion
        {
            Id = request.Id,
            ValidFrom = request.ValidFrom,
            LegalReference = request.LegalReference,
            Note = request.Note,
            UpdatedByUserId = userId,
            Regions = request.Regions.Select(r => new MinimumWageRegion { RegionCode = r.RegionCode, AmountPerMonth = r.AmountPerMonth }).ToList()
        };
        var result = await _repo.UpdateAsync(entity, ct);
        if (result) await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(MinimumWageVersion)), ct);
        return result;
    }
}

public sealed class DeleteMinimumWagePendingCommandHandler : IRequestHandler<DeleteMinimumWagePendingCommand, bool>
{
    private readonly IMinimumWageRepository _repo;
    private readonly IPublisher _publisher;

    public DeleteMinimumWagePendingCommandHandler(IMinimumWageRepository repo, IPublisher publisher) { _repo = repo; _publisher = publisher; }

    public async Task<bool> Handle(DeleteMinimumWagePendingCommand request, CancellationToken ct)
    {
        var result = await _repo.DeletePendingAsync(request.Id, ct);
        if (result) await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(MinimumWageVersion)), ct);
        return result;
    }
}
