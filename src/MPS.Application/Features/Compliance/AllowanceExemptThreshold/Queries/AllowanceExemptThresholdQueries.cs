using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Enums.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.AllowanceExemptThreshold.Queries;

public sealed record GetAllAllowanceExemptThresholdsQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<AllowanceExemptThresholdDto>>;
public sealed record GetAllowanceExemptThresholdByIdQuery(int Id) : IRequest<AllowanceExemptThresholdDto?>;
public sealed record GetActiveAllowanceExemptThresholdQuery(int AllowanceTypeId) : IRequest<AllowanceExemptThresholdDto?>;
public sealed record GetActiveAllowanceExemptThresholdAtQuery(int AllowanceTypeId, DateOnly Date) : IRequest<AllowanceExemptThresholdDto?>;

public sealed class GetAllAllowanceExemptThresholdsQueryHandler : IRequestHandler<GetAllAllowanceExemptThresholdsQuery, IReadOnlyList<AllowanceExemptThresholdDto>>
{
    private readonly IAllowanceExemptThresholdRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;
    public GetAllAllowanceExemptThresholdsQueryHandler(IAllowanceExemptThresholdRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    { _repo = repo; _legalDocumentRepo = legalDocumentRepo; _mapper = mapper; }

    public async Task<IReadOnlyList<AllowanceExemptThresholdDto>> Handle(GetAllAllowanceExemptThresholdsQuery request, CancellationToken ct)
    {
        var items = await _repo.GetAllAsync(request.IncludeArchived, ct);
        var dtos = _mapper.Map<IReadOnlyList<AllowanceExemptThresholdDto>>(items);

        var result = new List<AllowanceExemptThresholdDto>(dtos.Count);
        foreach (var dto in dtos)
        {
            var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.AllowanceExemptThreshold, dto.Id, ct);
            result.Add(dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) });
        }
        return result;
    }
}

public sealed class GetAllowanceExemptThresholdByIdQueryHandler : IRequestHandler<GetAllowanceExemptThresholdByIdQuery, AllowanceExemptThresholdDto?>
{
    private readonly IAllowanceExemptThresholdRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;
    public GetAllowanceExemptThresholdByIdQueryHandler(IAllowanceExemptThresholdRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    { _repo = repo; _legalDocumentRepo = legalDocumentRepo; _mapper = mapper; }

    public async Task<AllowanceExemptThresholdDto?> Handle(GetAllowanceExemptThresholdByIdQuery request, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(request.Id, ct);
        if (item is null) return null;
        var dto = _mapper.Map<AllowanceExemptThresholdDto>(item);
        var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.AllowanceExemptThreshold, dto.Id, ct);
        return dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) };
    }
}

public sealed class GetActiveAllowanceExemptThresholdQueryHandler : IRequestHandler<GetActiveAllowanceExemptThresholdQuery, AllowanceExemptThresholdDto?>
{
    private readonly IAllowanceExemptThresholdRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;
    public GetActiveAllowanceExemptThresholdQueryHandler(IAllowanceExemptThresholdRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    { _repo = repo; _legalDocumentRepo = legalDocumentRepo; _mapper = mapper; }

    public async Task<AllowanceExemptThresholdDto?> Handle(GetActiveAllowanceExemptThresholdQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveForAllowanceTypeAsync(request.AllowanceTypeId, ct);
        if (item is null) return null;
        var dto = _mapper.Map<AllowanceExemptThresholdDto>(item);
        var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.AllowanceExemptThreshold, dto.Id, ct);
        return dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) };
    }
}

public sealed class GetActiveAllowanceExemptThresholdAtQueryHandler : IRequestHandler<GetActiveAllowanceExemptThresholdAtQuery, AllowanceExemptThresholdDto?>
{
    private readonly IAllowanceExemptThresholdRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;
    public GetActiveAllowanceExemptThresholdAtQueryHandler(IAllowanceExemptThresholdRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    { _repo = repo; _legalDocumentRepo = legalDocumentRepo; _mapper = mapper; }

    public async Task<AllowanceExemptThresholdDto?> Handle(GetActiveAllowanceExemptThresholdAtQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveAtForAllowanceTypeAsync(request.AllowanceTypeId, request.Date, ct);
        if (item is null) return null;
        var dto = _mapper.Map<AllowanceExemptThresholdDto>(item);
        var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.AllowanceExemptThreshold, dto.Id, ct);
        return dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) };
    }
}
