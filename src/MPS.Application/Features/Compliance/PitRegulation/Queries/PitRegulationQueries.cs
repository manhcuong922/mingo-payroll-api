using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Enums.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.PitRegulation.Queries;

public sealed record GetAllPitRegulationsQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<PitRegulationDto>>;
public sealed record GetPitRegulationByIdQuery(int Id) : IRequest<PitRegulationDto?>;
public sealed record GetActivePitRegulationQuery : IRequest<PitRegulationDto?>;
public sealed record GetActivePitRegulationAtQuery(DateOnly Date) : IRequest<PitRegulationDto?>;

public sealed class GetAllPitRegulationsQueryHandler : IRequestHandler<GetAllPitRegulationsQuery, IReadOnlyList<PitRegulationDto>>
{
    private readonly IPitRegulationRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;
    public GetAllPitRegulationsQueryHandler(IPitRegulationRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    { _repo = repo; _legalDocumentRepo = legalDocumentRepo; _mapper = mapper; }

    public async Task<IReadOnlyList<PitRegulationDto>> Handle(GetAllPitRegulationsQuery request, CancellationToken ct)
    {
        var items = await _repo.GetAllAsync(request.IncludeArchived, ct);
        var dtos = _mapper.Map<IReadOnlyList<PitRegulationDto>>(items);

        var result = new List<PitRegulationDto>(dtos.Count);
        foreach (var dto in dtos)
        {
            var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.PitRegulation, dto.Id, ct);
            result.Add(dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) });
        }
        return result;
    }
}

public sealed class GetPitRegulationByIdQueryHandler : IRequestHandler<GetPitRegulationByIdQuery, PitRegulationDto?>
{
    private readonly IPitRegulationRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;
    public GetPitRegulationByIdQueryHandler(IPitRegulationRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    { _repo = repo; _legalDocumentRepo = legalDocumentRepo; _mapper = mapper; }

    public async Task<PitRegulationDto?> Handle(GetPitRegulationByIdQuery request, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(request.Id, ct);
        if (item is null) return null;
        var dto = _mapper.Map<PitRegulationDto>(item);
        var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.PitRegulation, dto.Id, ct);
        return dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) };
    }
}

public sealed class GetActivePitRegulationQueryHandler : IRequestHandler<GetActivePitRegulationQuery, PitRegulationDto?>
{
    private readonly IPitRegulationRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;
    public GetActivePitRegulationQueryHandler(IPitRegulationRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    { _repo = repo; _legalDocumentRepo = legalDocumentRepo; _mapper = mapper; }

    public async Task<PitRegulationDto?> Handle(GetActivePitRegulationQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveAsync(ct);
        if (item is null) return null;
        var dto = _mapper.Map<PitRegulationDto>(item);
        var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.PitRegulation, dto.Id, ct);
        return dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) };
    }
}

public sealed class GetActivePitRegulationAtQueryHandler : IRequestHandler<GetActivePitRegulationAtQuery, PitRegulationDto?>
{
    private readonly IPitRegulationRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;
    public GetActivePitRegulationAtQueryHandler(IPitRegulationRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    { _repo = repo; _legalDocumentRepo = legalDocumentRepo; _mapper = mapper; }

    public async Task<PitRegulationDto?> Handle(GetActivePitRegulationAtQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveAtAsync(request.Date, ct);
        if (item is null) return null;
        var dto = _mapper.Map<PitRegulationDto>(item);
        var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.PitRegulation, dto.Id, ct);
        return dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) };
    }
}
