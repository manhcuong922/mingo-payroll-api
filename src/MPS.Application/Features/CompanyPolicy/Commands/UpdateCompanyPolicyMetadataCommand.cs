using MediatR;
using MPS.Application.Common.Interfaces;
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

    public UpdateCompanyPolicyMetadataCommandHandler(
        ICompanyPolicyRepository repo,
        ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(UpdateCompanyPolicyMetadataCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new DomainValidationException("UPDATE_REQUIRES_AUTHENTICATION");

        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainValidationException("COMPANY_POLICY_TITLE_REQUIRED");

        return await _repo.UpdateMetadataAsync(
            request.Id, title, request.Description?.Trim(),
            request.IsActive, userId, DateTime.UtcNow, cancellationToken);
    }
}
