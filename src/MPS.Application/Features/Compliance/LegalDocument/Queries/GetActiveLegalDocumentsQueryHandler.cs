using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.LegalDocument.Queries;

public sealed class GetActiveLegalDocumentsQueryHandler : IRequestHandler<GetActiveLegalDocumentsQuery, IReadOnlyList<LegalDocumentDto>>
{
    private readonly ILegalDocumentRepository _repo;
    private readonly IMapper _mapper;

    public GetActiveLegalDocumentsQueryHandler(ILegalDocumentRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<LegalDocumentDto>> Handle(GetActiveLegalDocumentsQuery request, CancellationToken ct)
    {
        var items = await _repo.GetActiveAsync(ct);
        return _mapper.Map<IReadOnlyList<LegalDocumentDto>>(items);
    }
}
