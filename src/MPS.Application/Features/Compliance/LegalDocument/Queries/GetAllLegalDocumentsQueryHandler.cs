using AutoMapper;
using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.LegalDocument.Queries;

public sealed class GetAllLegalDocumentsQueryHandler : IRequestHandler<GetAllLegalDocumentsQuery, PagedResult<LegalDocumentDto>>
{
    private readonly ILegalDocumentRepository _repo;
    private readonly IMapper _mapper;

    public GetAllLegalDocumentsQueryHandler(ILegalDocumentRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<PagedResult<LegalDocumentDto>> Handle(GetAllLegalDocumentsQuery request, CancellationToken ct)
    {
        var (items, total) = await _repo.GetAllAsync(
            request.PageNumber, request.PageSize, request.DocumentType, request.IsActive, request.Keyword, ct);

        var dtos = _mapper.Map<IReadOnlyList<LegalDocumentDto>>(items);
        return new PagedResult<LegalDocumentDto>(dtos, total, request.PageNumber, request.PageSize);
    }
}
