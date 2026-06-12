using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Queries;

public sealed class GetAllSocialInsuranceRatesQueryHandler
    : IRequestHandler<GetAllSocialInsuranceRatesQuery, IReadOnlyList<SocialInsuranceRateDto>>
{
    private readonly ISocialInsuranceRateRepository _repo;
    private readonly IMapper _mapper;

    public GetAllSocialInsuranceRatesQueryHandler(ISocialInsuranceRateRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<SocialInsuranceRateDto>> Handle(GetAllSocialInsuranceRatesQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetAllAsync(request.IncludeArchived, cancellationToken);
        return _mapper.Map<IReadOnlyList<SocialInsuranceRateDto>>(items);
    }
}
