using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Queries;

public sealed class GetActiveSocialInsuranceRateQueryHandler
    : IRequestHandler<GetActiveSocialInsuranceRateQuery, SocialInsuranceRateDto?>
{
    private readonly ISocialInsuranceRateRepository _repo;
    private readonly IMapper _mapper;

    public GetActiveSocialInsuranceRateQueryHandler(ISocialInsuranceRateRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<SocialInsuranceRateDto?> Handle(GetActiveSocialInsuranceRateQuery request, CancellationToken cancellationToken)
    {
        var item = await _repo.GetActiveAsync(cancellationToken);
        return item is null ? null : _mapper.Map<SocialInsuranceRateDto>(item);
    }
}

public sealed class GetActiveSocialInsuranceRateAtQueryHandler
    : IRequestHandler<GetActiveSocialInsuranceRateAtQuery, SocialInsuranceRateDto?>
{
    private readonly ISocialInsuranceRateRepository _repo;
    private readonly IMapper _mapper;

    public GetActiveSocialInsuranceRateAtQueryHandler(ISocialInsuranceRateRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<SocialInsuranceRateDto?> Handle(GetActiveSocialInsuranceRateAtQuery request, CancellationToken cancellationToken)
    {
        var item = await _repo.GetActiveAtAsync(request.Date, cancellationToken);
        return item is null ? null : _mapper.Map<SocialInsuranceRateDto>(item);
    }
}
