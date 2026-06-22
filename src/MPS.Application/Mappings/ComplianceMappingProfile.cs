using AutoMapper;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Compliance;

namespace MPS.Application.Mappings;

public sealed class ComplianceMappingProfile : Profile
{
    private const string CreatedByParam = "CreatedBy";
    private const string UpdatedByParam = "UpdatedBy";
    private const string LegalDocumentsParam = "LegalDocuments";

    public ComplianceMappingProfile()
    {
        CreateMap<User, AuditUserDto>()
            .ForCtorParam("UserId", opt => opt.MapFrom(s => s.Id))
            .ForCtorParam("FullName", opt => opt.MapFrom(s => s.FullName))
            .ForCtorParam("Email", opt => opt.MapFrom(s => s.Email));

        CreateMap<SocialInsuranceFundEntry, SocialInsuranceFundEntryDto>();

#pragma warning disable CS0618 // Obsolete — still mapped for backward-compat DTO fields
        CreateMap<SocialInsuranceRateVersion, SocialInsuranceRateDto>()
            .ForCtorParam("Funds", opt => opt.MapFrom(s => s.Funds))
            .ForCtorParam("EligibleContractSubtypes", opt => opt.MapFrom(s =>
                string.IsNullOrWhiteSpace(s.EligibleContractSubtypesCsv)
                    ? Array.Empty<string>()
                    : s.EligibleContractSubtypesCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)))
            .ForCtorParam(LegalDocumentsParam, opt => opt.MapFrom(_ => Array.Empty<LegalDocumentSummaryDto>()))
            .ForCtorParam(CreatedByParam, opt => opt.MapFrom(s => s.CreatedByUser))
            .ForCtorParam(UpdatedByParam, opt => opt.MapFrom(s => s.UpdatedByUser));
#pragma warning restore CS0618

        CreateMap<PitBracket, PitBracketDto>();
        CreateMap<PitRegulationVersion, PitRegulationDto>()
            .ForCtorParam("Brackets", opt => opt.MapFrom(s => s.Brackets.OrderBy(b => b.Order)))
            .ForCtorParam(LegalDocumentsParam, opt => opt.MapFrom(_ => Array.Empty<LegalDocumentSummaryDto>()))
            .ForCtorParam(CreatedByParam, opt => opt.MapFrom(s => s.CreatedByUser))
            .ForCtorParam(UpdatedByParam, opt => opt.MapFrom(s => s.UpdatedByUser));

        CreateMap<MinimumWageRegion, MinimumWageRegionDto>();
        CreateMap<MinimumWageVersion, MinimumWageDto>()
            .ForCtorParam("Regions", opt => opt.MapFrom(s => s.Regions))
            .ForCtorParam(LegalDocumentsParam, opt => opt.MapFrom(_ => Array.Empty<LegalDocumentSummaryDto>()))
            .ForCtorParam(CreatedByParam, opt => opt.MapFrom(s => s.CreatedByUser))
            .ForCtorParam(UpdatedByParam, opt => opt.MapFrom(s => s.UpdatedByUser));

        CreateMap<AllowanceExemptThresholdVersion, AllowanceExemptThresholdDto>()
            .ForCtorParam("AllowanceTypeCode", opt => opt.MapFrom(s => s.AllowanceType != null ? s.AllowanceType.Code : string.Empty))
            .ForCtorParam("AllowanceTypeName", opt => opt.MapFrom(s => s.AllowanceType != null ? s.AllowanceType.Name : string.Empty))
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
