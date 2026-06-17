using AutoMapper;
using MediatR;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Enums.Compliance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.SocialInsuranceRate.Queries;

public sealed class GetActiveSocialInsuranceRateQueryHandler
    : IRequestHandler<GetActiveSocialInsuranceRateQuery, SocialInsuranceRateDto?>
{
    private readonly ISocialInsuranceRateRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;

    public GetActiveSocialInsuranceRateQueryHandler(
        ISocialInsuranceRateRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    {
        _repo = repo;
        _legalDocumentRepo = legalDocumentRepo;
        _mapper = mapper;
    }

    public async Task<SocialInsuranceRateDto?> Handle(GetActiveSocialInsuranceRateQuery request, CancellationToken cancellationToken)
    {
        var item = await _repo.GetActiveAsync(cancellationToken);
        if (item is null) return null;

        var dto = _mapper.Map<SocialInsuranceRateDto>(item);
        var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.SocialInsuranceRate, dto.Id, cancellationToken);
        return dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) };
    }
}

public sealed class GetActiveSocialInsuranceRateAtQueryHandler
    : IRequestHandler<GetActiveSocialInsuranceRateAtQuery, SocialInsuranceRateDto?>
{
    private readonly ISocialInsuranceRateRepository _repo;
    private readonly ILegalDocumentRepository _legalDocumentRepo;
    private readonly IMapper _mapper;

    public GetActiveSocialInsuranceRateAtQueryHandler(
        ISocialInsuranceRateRepository repo, ILegalDocumentRepository legalDocumentRepo, IMapper mapper)
    {
        _repo = repo;
        _legalDocumentRepo = legalDocumentRepo;
        _mapper = mapper;
    }

    public async Task<SocialInsuranceRateDto?> Handle(GetActiveSocialInsuranceRateAtQuery request, CancellationToken cancellationToken)
    {
        var item = await _repo.GetActiveAtAsync(request.Date, cancellationToken);
        if (item is null) return null;

        var dto = _mapper.Map<SocialInsuranceRateDto>(item);
        var docs = await _legalDocumentRepo.GetByComplianceVersionAsync(ComplianceEntityTypes.SocialInsuranceRate, dto.Id, cancellationToken);
        return dto with { LegalDocuments = _mapper.Map<IReadOnlyList<LegalDocumentSummaryDto>>(docs) };
    }
}
