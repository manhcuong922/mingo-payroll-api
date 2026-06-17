using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.LegalDocument.Commands;

public sealed class DeactivateLegalDocumentCommandHandler : IRequestHandler<DeactivateLegalDocumentCommand, bool>
{
    private readonly ILegalDocumentRepository _repo;

    public DeactivateLegalDocumentCommandHandler(ILegalDocumentRepository repo)
    {
        _repo = repo;
    }

    public async Task<bool> Handle(DeactivateLegalDocumentCommand request, CancellationToken ct) =>
        await _repo.DeactivateAsync(request.Id, ct);
}
