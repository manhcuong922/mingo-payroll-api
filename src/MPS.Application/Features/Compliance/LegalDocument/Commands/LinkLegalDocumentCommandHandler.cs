using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.LegalDocument.Commands;

public sealed class LinkLegalDocumentCommandHandler : IRequestHandler<LinkLegalDocumentCommand, int>
{
    private readonly ILegalDocumentRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public LinkLegalDocumentCommandHandler(ILegalDocumentRepository repo, ICurrentUserService currentUser, IClock clock)
    {
        _repo = repo;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<int> Handle(LinkLegalDocumentCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var link = new ComplianceVersionLegalDocument
        {
            ComplianceEntityType = request.ComplianceEntityType,
            ComplianceVersionId = request.ComplianceVersionId,
            LegalDocumentId = request.LegalDocumentId,
            LinkedByUserId = userId,
            LinkedAt = _clock.UtcNow,
        };

        return await _repo.LinkToVersionAsync(link, ct);
    }
}
