using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.LaborLawConstants.Queries;

public sealed record GetAllLaborLawConstantsQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<LaborLawConstantsDto>>;
public sealed record GetLaborLawConstantsByIdQuery(int Id) : IRequest<LaborLawConstantsDto?>;
public sealed record GetActiveLaborLawConstantsQuery : IRequest<LaborLawConstantsDto?>;
public sealed record GetActiveLaborLawConstantsAtQuery(DateOnly Date) : IRequest<LaborLawConstantsDto?>;

public sealed class GetAllLaborLawConstantsQueryHandler : IRequestHandler<GetAllLaborLawConstantsQuery, IReadOnlyList<LaborLawConstantsDto>>
{
    private readonly ILaborLawConstantsRepository _repo;
    private readonly IMapper _mapper;
    public GetAllLaborLawConstantsQueryHandler(ILaborLawConstantsRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<IReadOnlyList<LaborLawConstantsDto>> Handle(GetAllLaborLawConstantsQuery request, CancellationToken ct)
        => _mapper.Map<IReadOnlyList<LaborLawConstantsDto>>(await _repo.GetAllAsync(request.IncludeArchived, ct));
}

public sealed class GetLaborLawConstantsByIdQueryHandler : IRequestHandler<GetLaborLawConstantsByIdQuery, LaborLawConstantsDto?>
{
    private readonly ILaborLawConstantsRepository _repo;
    private readonly IMapper _mapper;
    public GetLaborLawConstantsByIdQueryHandler(ILaborLawConstantsRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<LaborLawConstantsDto?> Handle(GetLaborLawConstantsByIdQuery request, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(request.Id, ct);
        return item is null ? null : _mapper.Map<LaborLawConstantsDto>(item);
    }
}

public sealed class GetActiveLaborLawConstantsQueryHandler : IRequestHandler<GetActiveLaborLawConstantsQuery, LaborLawConstantsDto?>
{
    private readonly ILaborLawConstantsRepository _repo;
    private readonly IMapper _mapper;
    public GetActiveLaborLawConstantsQueryHandler(ILaborLawConstantsRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<LaborLawConstantsDto?> Handle(GetActiveLaborLawConstantsQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveAsync(ct);
        return item is null ? null : _mapper.Map<LaborLawConstantsDto>(item);
    }
}

public sealed class GetActiveLaborLawConstantsAtQueryHandler : IRequestHandler<GetActiveLaborLawConstantsAtQuery, LaborLawConstantsDto?>
{
    private readonly ILaborLawConstantsRepository _repo;
    private readonly IMapper _mapper;
    public GetActiveLaborLawConstantsAtQueryHandler(ILaborLawConstantsRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<LaborLawConstantsDto?> Handle(GetActiveLaborLawConstantsAtQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveAtAsync(request.Date, ct);
        return item is null ? null : _mapper.Map<LaborLawConstantsDto>(item);
    }
}
