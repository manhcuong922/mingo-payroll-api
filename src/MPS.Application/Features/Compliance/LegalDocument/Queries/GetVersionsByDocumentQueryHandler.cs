using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.LegalDocument.Queries;

public sealed class GetVersionsByDocumentQueryHandler
    : IRequestHandler<GetVersionsByDocumentQuery, IReadOnlyList<ComplianceVersionWithDocDto>>
{
    private readonly ILegalDocumentRepository _repo;

    public GetVersionsByDocumentQueryHandler(ILegalDocumentRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<ComplianceVersionWithDocDto>> Handle(GetVersionsByDocumentQuery request, CancellationToken ct)
    {
        var pairs = await _repo.GetVersionsByDocumentAsync(request.LegalDocumentId, ct);
        return pairs.Select(p => new ComplianceVersionWithDocDto(p.EntityType, p.VersionId)).ToList();
    }
}
