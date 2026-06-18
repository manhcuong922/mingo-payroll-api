using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Enums.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Queries;

public sealed class GetAllSocialInsuranceRatesQueryHandler
    : IRequestHandler<GetAllSocialInsuranceRatesQuery, IReadOnlyList<SocialInsuranceRateDto>>
{
    private readonly ISocialInsuranceRateRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;

    public GetAllSocialInsuranceRatesQueryHandler(
        ISocialInsuranceRateRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    {
        _repo = repo;
        _legalDocumentRepo = legalDocumentRepo;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<SocialInsuranceRateDto>> Handle(GetAllSocialInsuranceRatesQuery request, CancellationToken cancellationToken)
    {
        var items = await _repo.GetAllAsync(request.IncludeArchived, cancellationToken);
        var dtos = _mapper.Map<IReadOnlyList<SocialInsuranceRateDto>>(items);

        var result = new List<SocialInsuranceRateDto>(dtos.Count);
        foreach (var dto in dtos)
        {
            var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.SocialInsuranceRate, dto.Id, cancellationToken);
            result.Add(dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) });
        }

        return result;
    }
}
