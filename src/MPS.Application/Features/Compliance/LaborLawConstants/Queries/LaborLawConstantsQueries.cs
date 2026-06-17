using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Enums.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.LaborLawConstants.Queries;

public sealed record GetAllLaborLawConstantsQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<LaborLawConstantsDto>>;
public sealed record GetLaborLawConstantsByIdQuery(int Id) : IRequest<LaborLawConstantsDto?>;
public sealed record GetActiveLaborLawConstantsQuery : IRequest<LaborLawConstantsDto?>;
public sealed record GetActiveLaborLawConstantsAtQuery(DateOnly Date) : IRequest<LaborLawConstantsDto?>;

public sealed class GetAllLaborLawConstantsQueryHandler : IRequestHandler<GetAllLaborLawConstantsQuery, IReadOnlyList<LaborLawConstantsDto>>
{
    private readonly ILaborLawConstantsRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;
    public GetAllLaborLawConstantsQueryHandler(ILaborLawConstantsRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    { _repo = repo; _legalDocumentRepo = legalDocumentRepo; _mapper = mapper; }

    public async Task<IReadOnlyList<LaborLawConstantsDto>> Handle(GetAllLaborLawConstantsQuery request, CancellationToken ct)
    {
        var items = await _repo.GetAllAsync(request.IncludeArchived, ct);
        var dtos = _mapper.Map<IReadOnlyList<LaborLawConstantsDto>>(items);

        var result = new List<LaborLawConstantsDto>(dtos.Count);
        foreach (var dto in dtos)
        {
            var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.LaborLawConstants, dto.Id, ct);
            result.Add(dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) });
        }
        return result;
    }
}

public sealed class GetLaborLawConstantsByIdQueryHandler : IRequestHandler<GetLaborLawConstantsByIdQuery, LaborLawConstantsDto?>
{
    private readonly ILaborLawConstantsRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;
    public GetLaborLawConstantsByIdQueryHandler(ILaborLawConstantsRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    { _repo = repo; _legalDocumentRepo = legalDocumentRepo; _mapper = mapper; }

    public async Task<LaborLawConstantsDto?> Handle(GetLaborLawConstantsByIdQuery request, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(request.Id, ct);
        if (item is null) return null;
        var dto = _mapper.Map<LaborLawConstantsDto>(item);
        var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.LaborLawConstants, dto.Id, ct);
        return dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) };
    }
}

public sealed class GetActiveLaborLawConstantsQueryHandler : IRequestHandler<GetActiveLaborLawConstantsQuery, LaborLawConstantsDto?>
{
    private readonly ILaborLawConstantsRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;
    public GetActiveLaborLawConstantsQueryHandler(ILaborLawConstantsRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    { _repo = repo; _legalDocumentRepo = legalDocumentRepo; _mapper = mapper; }

    public async Task<LaborLawConstantsDto?> Handle(GetActiveLaborLawConstantsQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveAsync(ct);
        if (item is null) return null;
        var dto = _mapper.Map<LaborLawConstantsDto>(item);
        var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.LaborLawConstants, dto.Id, ct);
        return dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) };
    }
}

public sealed class GetActiveLaborLawConstantsAtQueryHandler : IRequestHandler<GetActiveLaborLawConstantsAtQuery, LaborLawConstantsDto?>
{
    private readonly ILaborLawConstantsRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;
    public GetActiveLaborLawConstantsAtQueryHandler(ILaborLawConstantsRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    { _repo = repo; _legalDocumentRepo = legalDocumentRepo; _mapper = mapper; }

    public async Task<LaborLawConstantsDto?> Handle(GetActiveLaborLawConstantsAtQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveAtAsync(request.Date, ct);
        if (item is null) return null;
        var dto = _mapper.Map<LaborLawConstantsDto>(item);
        var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.LaborLawConstants, dto.Id, ct);
        return dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) };
    }
}
