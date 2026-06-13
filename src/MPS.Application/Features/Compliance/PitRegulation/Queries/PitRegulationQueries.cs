using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.PitRegulation.Queries;

public sealed record GetAllPitRegulationsQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<PitRegulationDto>>;
public sealed record GetPitRegulationByIdQuery(int Id) : IRequest<PitRegulationDto?>;
public sealed record GetActivePitRegulationQuery : IRequest<PitRegulationDto?>;
public sealed record GetActivePitRegulationAtQuery(DateOnly Date) : IRequest<PitRegulationDto?>;

public sealed class GetAllPitRegulationsQueryHandler : IRequestHandler<GetAllPitRegulationsQuery, IReadOnlyList<PitRegulationDto>>
{
    private readonly IPitRegulationRepository _repo;
    private readonly IMapper _mapper;
    public GetAllPitRegulationsQueryHandler(IPitRegulationRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<IReadOnlyList<PitRegulationDto>> Handle(GetAllPitRegulationsQuery request, CancellationToken ct)
    {
        var items = await _repo.GetAllAsync(request.IncludeArchived, ct);
        return _mapper.Map<IReadOnlyList<PitRegulationDto>>(items);
    }
}

public sealed class GetPitRegulationByIdQueryHandler : IRequestHandler<GetPitRegulationByIdQuery, PitRegulationDto?>
{
    private readonly IPitRegulationRepository _repo;
    private readonly IMapper _mapper;
    public GetPitRegulationByIdQueryHandler(IPitRegulationRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<PitRegulationDto?> Handle(GetPitRegulationByIdQuery request, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(request.Id, ct);
        return item is null ? null : _mapper.Map<PitRegulationDto>(item);
    }
}

public sealed class GetActivePitRegulationQueryHandler : IRequestHandler<GetActivePitRegulationQuery, PitRegulationDto?>
{
    private readonly IPitRegulationRepository _repo;
    private readonly IMapper _mapper;
    public GetActivePitRegulationQueryHandler(IPitRegulationRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<PitRegulationDto?> Handle(GetActivePitRegulationQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveAsync(ct);
        return item is null ? null : _mapper.Map<PitRegulationDto>(item);
    }
}

public sealed class GetActivePitRegulationAtQueryHandler : IRequestHandler<GetActivePitRegulationAtQuery, PitRegulationDto?>
{
    private readonly IPitRegulationRepository _repo;
    private readonly IMapper _mapper;
    public GetActivePitRegulationAtQueryHandler(IPitRegulationRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<PitRegulationDto?> Handle(GetActivePitRegulationAtQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveAtAsync(request.Date, ct);
        return item is null ? null : _mapper.Map<PitRegulationDto>(item);
    }
}
