using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Queries;

public sealed class GetSocialInsuranceRateByIdQueryHandler
    : IRequestHandler<GetSocialInsuranceRateByIdQuery, SocialInsuranceRateDto?>
{
    private readonly ISocialInsuranceRateRepository _repo;
    private readonly IMapper _mapper;

    public GetSocialInsuranceRateByIdQueryHandler(ISocialInsuranceRateRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<SocialInsuranceRateDto?> Handle(GetSocialInsuranceRateByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await _repo.GetByIdAsync(request.Id, cancellationToken);
        return item is null ? null : _mapper.Map<SocialInsuranceRateDto>(item);
    }
}
