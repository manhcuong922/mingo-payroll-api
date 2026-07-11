using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Notifications;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.CompanyPolicy.Commands;

public sealed record UpdateCompanyPolicyMetadataCommand(
    int Id,
    string Title,
    string? Description,
    bool IsActive) : IRequest<bool>;

public sealed class UpdateCompanyPolicyMetadataCommandHandler
    : IRequestHandler<UpdateCompanyPolicyMetadataCommand, bool>
{
    private readonly ICompanyPolicyRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;

    public UpdateCompanyPolicyMetadataCommandHandler(
        ICompanyPolicyRepository repo,
        ICurrentUserService currentUser,
        IPublisher publisher)
    {
        _repo = repo;
        _currentUser = currentUser;
        _publisher = publisher;
    }

    public async Task<bool> Handle(UpdateCompanyPolicyMetadataCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new DomainValidationException("UPDATE_REQUIRES_AUTHENTICATION");

        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainValidationException("COMPANY_POLICY_TITLE_REQUIRED");

        var updated = await _repo.UpdateMetadataAsync(
            request.Id, title, request.Description?.Trim(),
            request.IsActive, userId, DateTime.UtcNow, cancellationToken);

        if (updated)
            await _publisher.Publish(
                new CompanyPolicyChangedNotification(request.Id, title, CompanyPolicyChangeKind.Updated),
                cancellationToken);

        return updated;
    }
}
