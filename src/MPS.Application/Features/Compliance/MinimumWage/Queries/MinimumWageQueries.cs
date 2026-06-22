using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Enums.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.MinimumWage.Queries;

public sealed record GetAllMinimumWagesQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<MinimumWageDto>>;
public sealed record GetMinimumWageByIdQuery(int Id) : IRequest<MinimumWageDto?>;
public sealed record GetActiveMinimumWageQuery : IRequest<MinimumWageDto?>;
public sealed record GetActiveMinimumWageAtQuery(DateOnly Date) : IRequest<MinimumWageDto?>;

public sealed class GetAllMinimumWagesQueryHandler : IRequestHandler<GetAllMinimumWagesQuery, IReadOnlyList<MinimumWageDto>>
{
    private readonly IMinimumWageRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;
    public GetAllMinimumWagesQueryHandler(IMinimumWageRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    { _repo = repo; _legalDocumentRepo = legalDocumentRepo; _mapper = mapper; }

    public async Task<IReadOnlyList<MinimumWageDto>> Handle(GetAllMinimumWagesQuery request, CancellationToken ct)
    {
        var items = await _repo.GetAllAsync(request.IncludeArchived, ct);
        var dtos = _mapper.Map<IReadOnlyList<MinimumWageDto>>(items);

        var result = new List<MinimumWageDto>(dtos.Count);
        foreach (var dto in dtos)
        {
            var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.MinimumWage, dto.Id, ct);
            result.Add(dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) });
        }
        return result;
    }
}

public sealed class GetMinimumWageByIdQueryHandler : IRequestHandler<GetMinimumWageByIdQuery, MinimumWageDto?>
{
    private readonly IMinimumWageRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;
    public GetMinimumWageByIdQueryHandler(IMinimumWageRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    { _repo = repo; _legalDocumentRepo = legalDocumentRepo; _mapper = mapper; }

    public async Task<MinimumWageDto?> Handle(GetMinimumWageByIdQuery request, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(request.Id, ct);
        if (item is null) return null;
        var dto = _mapper.Map<MinimumWageDto>(item);
        var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.MinimumWage, dto.Id, ct);
        return dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) };
    }
}

public sealed class GetActiveMinimumWageQueryHandler : IRequestHandler<GetActiveMinimumWageQuery, MinimumWageDto?>
{
    private readonly IMinimumWageRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;
    public GetActiveMinimumWageQueryHandler(IMinimumWageRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    { _repo = repo; _legalDocumentRepo = legalDocumentRepo; _mapper = mapper; }

    public async Task<MinimumWageDto?> Handle(GetActiveMinimumWageQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveAsync(ct);
        if (item is null) return null;
        var dto = _mapper.Map<MinimumWageDto>(item);
        var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.MinimumWage, dto.Id, ct);
        return dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) };
    }
}

public sealed class GetActiveMinimumWageAtQueryHandler : IRequestHandler<GetActiveMinimumWageAtQuery, MinimumWageDto?>
{
    private readonly IMinimumWageRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;
    public GetActiveMinimumWageAtQueryHandler(IMinimumWageRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    { _repo = repo; _legalDocumentRepo = legalDocumentRepo; _mapper = mapper; }

    public async Task<MinimumWageDto?> Handle(GetActiveMinimumWageAtQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveAtAsync(request.Date, ct);
        if (item is null) return null;
        var dto = _mapper.Map<MinimumWageDto>(item);
        var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.MinimumWage, dto.Id, ct);
        return dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) };
    }
}
