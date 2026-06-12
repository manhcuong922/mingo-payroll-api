using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.OtRate.Queries;

public sealed record GetAllOtRatesQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<OtRateDto>>;
public sealed record GetOtRateByIdQuery(int Id) : IRequest<OtRateDto?>;
public sealed record GetActiveOtRateQuery : IRequest<OtRateDto?>;
public sealed record GetActiveOtRateAtQuery(DateOnly Date) : IRequest<OtRateDto?>;

public sealed class GetAllOtRatesQueryHandler : IRequestHandler<GetAllOtRatesQuery, IReadOnlyList<OtRateDto>>
{
    private readonly IOtRateRepository _repo;
    private readonly IMapper _mapper;
    public GetAllOtRatesQueryHandler(IOtRateRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<IReadOnlyList<OtRateDto>> Handle(GetAllOtRatesQuery request, CancellationToken ct)
        => _mapper.Map<IReadOnlyList<OtRateDto>>(await _repo.GetAllAsync(request.IncludeArchived, ct));
}

public sealed class GetOtRateByIdQueryHandler : IRequestHandler<GetOtRateByIdQuery, OtRateDto?>
{
    private readonly IOtRateRepository _repo;
    private readonly IMapper _mapper;
    public GetOtRateByIdQueryHandler(IOtRateRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<OtRateDto?> Handle(GetOtRateByIdQuery request, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(request.Id, ct);
        return item is null ? null : _mapper.Map<OtRateDto>(item);
    }
}

public sealed class GetActiveOtRateQueryHandler : IRequestHandler<GetActiveOtRateQuery, OtRateDto?>
{
    private readonly IOtRateRepository _repo;
    private readonly IMapper _mapper;
    public GetActiveOtRateQueryHandler(IOtRateRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<OtRateDto?> Handle(GetActiveOtRateQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveAsync(ct);
        return item is null ? null : _mapper.Map<OtRateDto>(item);
    }
}

public sealed class GetActiveOtRateAtQueryHandler : IRequestHandler<GetActiveOtRateAtQuery, OtRateDto?>
{
    private readonly IOtRateRepository _repo;
    private readonly IMapper _mapper;
    public GetActiveOtRateAtQueryHandler(IOtRateRepository repo, IMapper mapper) { _repo = repo; _mapper = mapper; }
    public async Task<OtRateDto?> Handle(GetActiveOtRateAtQuery request, CancellationToken ct)
    {
        var item = await _repo.GetActiveAtAsync(request.Date, ct);
        return item is null ? null : _mapper.Map<OtRateDto>(item);
    }
}
