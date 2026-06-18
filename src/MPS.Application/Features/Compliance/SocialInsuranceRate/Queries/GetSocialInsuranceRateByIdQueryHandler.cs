using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Enums.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Queries;

public sealed class GetSocialInsuranceRateByIdQueryHandler
    : IRequestHandler<GetSocialInsuranceRateByIdQuery, SocialInsuranceRateDto?>
{
    private readonly ISocialInsuranceRateRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;

    public GetSocialInsuranceRateByIdQueryHandler(
        ISocialInsuranceRateRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    {
        _repo = repo;
        _legalDocumentRepo = legalDocumentRepo;
        _mapper = mapper;
    }

    public async Task<SocialInsuranceRateDto?> Handle(GetSocialInsuranceRateByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await _repo.GetByIdAsync(request.Id, cancellationToken);
        if (item is null) return null;

        var dto = _mapper.Map<SocialInsuranceRateDto>(item);
        var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.SocialInsuranceRate, dto.Id, cancellationToken);
        return dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) };
    }
}
