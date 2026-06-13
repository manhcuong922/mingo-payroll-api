using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.OtRate.Commands;

public sealed class CreateOtRateCommandHandler : IRequestHandler<CreateOtRateCommand, int>
{
    private readonly IOtRateRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public CreateOtRateCommandHandler(IOtRateRepository repo, ICurrentUserService currentUser, IClock clock, IPublisher publisher)
    {
        _repo = repo; _currentUser = currentUser; _clock = clock; _publisher = publisher;
    }

    public async Task<int> Handle(CreateOtRateCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var entity = new OtRateVersion
        {
            ValidFrom = request.ValidFrom,
            LegalReference = request.LegalReference,
            Note = request.Note,
            CreatedByUserId = userId,
            CreatedAt = _clock.UtcNow,
            Entries = request.Entries.Select(e => new OtRateEntry { OtType = e.OtType, Label = e.Label, Rate = e.Rate }).ToList()
        };
        var id = await _repo.AddAsync(entity, request.ApplyImmediately, ct);
        await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(OtRateVersion)), ct);
        return id;
    }
}

public sealed class UpdateOtRateCommandHandler : IRequestHandler<UpdateOtRateCommand, bool>
{
    private readonly IOtRateRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;

    public UpdateOtRateCommandHandler(IOtRateRepository repo, ICurrentUserService currentUser, IClock clock, IPublisher publisher)
    {
        _repo = repo; _currentUser = currentUser; _clock = clock; _publisher = publisher;
    }

    public async Task<bool> Handle(UpdateOtRateCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var entity = new OtRateVersion
        {
            Id = request.Id,
            ValidFrom = request.ValidFrom,
            LegalReference = request.LegalReference,
            Note = request.Note,
            UpdatedByUserId = userId,
            Entries = request.Entries.Select(e => new OtRateEntry { OtType = e.OtType, Label = e.Label, Rate = e.Rate }).ToList()
        };
        var result = await _repo.UpdateAsync(entity, ct);
        if (result) await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(OtRateVersion)), ct);
        return result;
    }
}

public sealed class DeleteOtRatePendingCommandHandler : IRequestHandler<DeleteOtRatePendingCommand, bool>
{
    private readonly IOtRateRepository _repo;
    private readonly IPublisher _publisher;

    public DeleteOtRatePendingCommandHandler(IOtRateRepository repo, IPublisher publisher) { _repo = repo; _publisher = publisher; }

    public async Task<bool> Handle(DeleteOtRatePendingCommand request, CancellationToken ct)
    {
        var result = await _repo.DeletePendingAsync(request.Id, ct);
        if (result) await _publisher.Publish(new ComplianceVersionChangedNotification(nameof(OtRateVersion)), ct);
        return result;
    }
}
