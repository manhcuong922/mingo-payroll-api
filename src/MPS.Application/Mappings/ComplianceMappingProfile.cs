using AutoMapper;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Compliance;

namespace MPS.Application.Mappings;

public sealed class ComplianceMappingProfile : Profile
{
    private const string CreatedByParam = "createdBy";
    private const string UpdatedByParam = "updatedBy";
    private const string LegalDocumentsParam = "legalDocuments";

    public ComplianceMappingProfile()
    {
        CreateMap<User, AuditUserDto>()
            .ForCtorParam("userId", opt => opt.MapFrom(s => s.Id))
            .ForCtorParam("fullName", opt => opt.MapFrom(s => s.FullName))
            .ForCtorParam("email", opt => opt.MapFrom(s => s.Email));

        CreateMap<SocialInsuranceFundEntry, SocialInsuranceFundEntryDto>();

#pragma warning disable CS0618 // Obsolete — still mapped for backward-compat DTO fields
        CreateMap<SocialInsuranceRateVersion, SocialInsuranceRateDto>()
            .ForCtorParam("funds", opt => opt.MapFrom(s => s.Funds))
            .ForCtorParam("eligibleContractSubtypes", opt => opt.MapFrom(s =>
                string.IsNullOrWhiteSpace(s.EligibleContractSubtypesCsv)
                    ? Array.Empty<string>()
                    : s.EligibleContractSubtypesCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)))
            .ForCtorParam(LegalDocumentsParam, opt => opt.MapFrom(_ => Array.Empty<LegalDocumentSummaryDto>()))
            .ForCtorParam(CreatedByParam, opt => opt.MapFrom(s => s.CreatedByUser))
            .ForCtorParam(UpdatedByParam, opt => opt.MapFrom(s => s.UpdatedByUser));
#pragma warning restore CS0618

        CreateMap<PitBracket, PitBracketDto>();
        CreateMap<PitRegulationVersion, PitRegulationDto>()
            .ForCtorParam("brackets", opt => opt.MapFrom(s => s.Brackets.OrderBy(b => b.Order)))
            .ForCtorParam(LegalDocumentsParam, opt => opt.MapFrom(_ => Array.Empty<LegalDocumentSummaryDto>()))
            .ForCtorParam(CreatedByParam, opt => opt.MapFrom(s => s.CreatedByUser))
            .ForCtorParam(UpdatedByParam, opt => opt.MapFrom(s => s.UpdatedByUser));

        CreateMap<MinimumWageRegion, MinimumWageRegionDto>();
        CreateMap<MinimumWageVersion, MinimumWageDto>()
            .ForCtorParam("regions", opt => opt.MapFrom(s => s.Regions))
            .ForCtorParam(LegalDocumentsParam, opt => opt.MapFrom(_ => Array.Empty<LegalDocumentSummaryDto>()))
            .ForCtorParam(CreatedByParam, opt => opt.MapFrom(s => s.CreatedByUser))
            .ForCtorParam(UpdatedByParam, opt => opt.MapFrom(s => s.UpdatedByUser));

        CreateMap<AllowanceExemptThresholdVersion, AllowanceExemptThresholdDto>()
            .ForCtorParam("allowanceTypeCode", opt => opt.MapFrom(s => s.AllowanceType != null ? s.AllowanceType.Code : string.Empty))
            .ForCtorParam("allowanceTypeName", opt => opt.MapFrom(s => s.AllowanceType != null ? s.AllowanceType.Name : string.Empty))
            .ForCtorParam(LegalDocumentsParam, opt => opt.MapFrom(_ => Array.Empty<LegalDocumentSummaryDto>()))
            .ForCtorParam(CreatedByParam, opt => opt.MapFrom(s => s.CreatedByUser))
            .ForCtorParam(UpdatedByParam, opt => opt.MapFrom(s => s.UpdatedByUser));

        CreateMap<LaborLawConstantsVersion, LaborLawConstantsDto>()
            .ForCtorParam(LegalDocumentsParam, opt => opt.MapFrom(_ => Array.Empty<LegalDocumentSummaryDto>()))
            .ForCtorParam(CreatedByParam, opt => opt.MapFrom(s => s.CreatedByUser))
            .ForCtorParam(UpdatedByParam, opt => opt.MapFrom(s => s.UpdatedByUser));

        CreateMap<LegalDocument, LegalDocumentDto>()
            .ForCtorParam(CreatedByParam, opt => opt.MapFrom(s => s.CreatedByUser))
            .ForCtorParam(UpdatedByParam, opt => opt.MapFrom(s => s.UpdatedByUser));

        CreateMap<LegalDocument, LegalDocumentSummaryDto>();
    }
}
