using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.AllowanceExemptThreshold.Queries;

public sealed record GetAllAllowanceExemptThresholdsQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<AllowanceExemptThresholdDto>>;
public sealed record GetAllowanceExemptThresholdByIdQuery(int Id) : IRequest<AllowanceExemptThresholdDto?>;
public sealed record GetActiveAllowanceExemptThresholdQuery(int AllowanceTypeId) : IRequest<AllowanceExemptThresholdDto?>;
public sealed record GetActiveAllowanceExemptThresholdAtQuery(int AllowanceTypeId, DateOnly Date) : IRequest<AllowanceExemptThresholdDto?>;

public sealed class GetAllAllowanceExemptThresholdsQueryHandler : IRequestHandler<GetAllAllowanceExemptThresholdsQuery, IReadOnlyList<AllowanceExemptThresholdDto>>
{
    private readonly IAllowanceExemptThresholdRepository _repo;
    private readonly IMapper _mapper;
    public GetAllAllowanceExemptThresholdsQueryHandler(IAllowanceExemptThresholdRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<IReadOnlyList<AllowanceExemptThresholdDto>> Handle(GetAllAllowanceExemptThresholdsQuery request, CancellationToken ct)
        => _mapper.Map<IReadOnlyList<AllowanceExemptThresholdDto>>(await _repo.GetAllAsync(request.IncludeArchived, ct));
}

public sealed class GetAllowanceExemptThresholdByIdQueryHandler : IRequestHandler<GetAllowanceExemptThresholdByIdQuery, AllowanceExemptThresholdDto?>
{
    private readonly IAllowanceExemptThresholdRepository _repo;
    private readonly IMapper _mapper;
    public GetAllowanceExemptThresholdByIdQueryHandler(IAllowanceExemptThresholdRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<AllowanceExemptThresholdDto?> Handle(GetAllowanceExemptThresholdByIdQuery request, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(request.Id, ct);
        return item is null ? null : _mapper.Map<AllowanceExemptThresholdDto>(item);
    }
}

public sealed class GetActiveAllowanceExemptThresholdQueryHandler : IRequestHandler<GetActiveAllowanceExemptThresholdQuery, AllowanceExemptThresholdDto?>
{
    private readonly IAllowanceExemptThresholdRepository _repo;
    private readonly IMapper _mapper;
    public GetActiveAllowanceExemptThresholdQueryHandler(IAllowanceExemptThresholdRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<AllowanceExemptThresholdDto?> Handle(GetActiveAllowanceExemptThresholdQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveForAllowanceTypeAsync(request.AllowanceTypeId, ct);
        return item is null ? null : _mapper.Map<AllowanceExemptThresholdDto>(item);
    }
}

public sealed class GetActiveAllowanceExemptThresholdAtQueryHandler : IRequestHandler<GetActiveAllowanceExemptThresholdAtQuery, AllowanceExemptThresholdDto?>
{
    private readonly IAllowanceExemptThresholdRepository _repo;
    private readonly IMapper _mapper;
    public GetActiveAllowanceExemptThresholdAtQueryHandler(IAllowanceExemptThresholdRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<AllowanceExemptThresholdDto?> Handle(GetActiveAllowanceExemptThresholdAtQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveAtForAllowanceTypeAsync(request.AllowanceTypeId, request.Date, ct);
        return item is null ? null : _mapper.Map<AllowanceExemptThresholdDto>(item);
    }
}
