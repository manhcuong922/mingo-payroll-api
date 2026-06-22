using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.LegalDocument.Queries;

public sealed class GetLegalDocumentByIdQueryHandler : IRequestHandler<GetLegalDocumentByIdQuery, LegalDocumentDto?>
{
    private readonly ILegalDocumentRepository _repo;
    private readonly IMapper _mapper;

    public GetLegalDocumentByIdQueryHandler(ILegalDocumentRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<LegalDocumentDto?> Handle(GetLegalDocumentByIdQuery request, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(request.Id, ct);
        return item is null ? null : _mapper.Map<LegalDocumentDto>(item);
    }
}
