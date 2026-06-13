using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.MinimumWage.Queries;

public sealed record GetAllMinimumWagesQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<MinimumWageDto>>;
public sealed record GetMinimumWageByIdQuery(int Id) : IRequest<MinimumWageDto?>;
public sealed record GetActiveMinimumWageQuery : IRequest<MinimumWageDto?>;
public sealed record GetActiveMinimumWageAtQuery(DateOnly Date) : IRequest<MinimumWageDto?>;

public sealed class GetAllMinimumWagesQueryHandler : IRequestHandler<GetAllMinimumWagesQuery, IReadOnlyList<MinimumWageDto>>
{
    private readonly IMinimumWageRepository _repo;
    private readonly IMapper _mapper;
    public GetAllMinimumWagesQueryHandler(IMinimumWageRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<IReadOnlyList<MinimumWageDto>> Handle(GetAllMinimumWagesQuery request, CancellationToken ct)
        => _mapper.Map<IReadOnlyList<MinimumWageDto>>(await _repo.GetAllAsync(request.IncludeArchived, ct));
}

public sealed class GetMinimumWageByIdQueryHandler : IRequestHandler<GetMinimumWageByIdQuery, MinimumWageDto?>
{
    private readonly IMinimumWageRepository _repo;
    private readonly IMapper _mapper;
    public GetMinimumWageByIdQueryHandler(IMinimumWageRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<MinimumWageDto?> Handle(GetMinimumWageByIdQuery request, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(request.Id, ct);
        return item is null ? null : _mapper.Map<MinimumWageDto>(item);
    }
}

public sealed class GetActiveMinimumWageQueryHandler : IRequestHandler<GetActiveMinimumWageQuery, MinimumWageDto?>
{
    private readonly IMinimumWageRepository _repo;
    private readonly IMapper _mapper;
    public GetActiveMinimumWageQueryHandler(IMinimumWageRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<MinimumWageDto?> Handle(GetActiveMinimumWageQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveAsync(ct);
        return item is null ? null : _mapper.Map<MinimumWageDto>(item);
    }
}

public sealed class GetActiveMinimumWageAtQueryHandler : IRequestHandler<GetActiveMinimumWageAtQuery, MinimumWageDto?>
{
    private readonly IMinimumWageRepository _repo;
    private readonly IMapper _mapper;
    public GetActiveMinimumWageAtQueryHandler(IMinimumWageRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<MinimumWageDto?> Handle(GetActiveMinimumWageAtQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveAtAsync(request.Date, ct);
        return item is null ? null : _mapper.Map<MinimumWageDto>(item);
    }
}
