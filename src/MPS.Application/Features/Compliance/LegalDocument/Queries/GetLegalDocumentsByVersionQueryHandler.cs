using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.LegalDocument.Queries;

public sealed class GetLegalDocumentsByVersionQueryHandler
    : IRequestHandler<GetLegalDocumentsByVersionQuery, IReadOnlyList<LegalDocumentSummaryDto>>
{
    private readonly ILegalDocumentRepository _repo;
    private readonly IMapper _mapper;

    public GetLegalDocumentsByVersionQueryHandler(ILegalDocumentRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<LegalDocumentSummaryDto>> Handle(GetLegalDocumentsByVersionQuery request, CancellationToken ct)
    {
        var items = await _repo.GetByComplianceVersionAsync(request.ComplianceEntityType, request.ComplianceVersionId, ct);
        return _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(items);
    }
}
