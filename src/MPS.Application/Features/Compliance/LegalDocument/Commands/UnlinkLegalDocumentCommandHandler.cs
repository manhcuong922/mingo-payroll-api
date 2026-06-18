using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.LegalDocument.Commands;

public sealed class UnlinkLegalDocumentCommandHandler : IRequestHandler<UnlinkLegalDocumentCommand, bool>
{
    private readonly ILegalDocumentRepository _repo;

    public UnlinkLegalDocumentCommandHandler(ILegalDocumentRepository repo)
    {
        _repo = repo;
    }

    public async Task<bool> Handle(UnlinkLegalDocumentCommand request, CancellationToken ct) =>
        await _repo.UnlinkFromVersionAsync(request.ComplianceEntityType, request.ComplianceVersionId, request.LegalDocumentId, ct);
}
