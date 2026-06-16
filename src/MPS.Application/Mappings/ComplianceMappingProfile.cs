using AutoMapper;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Compliance;

namespace MPS.Application.Mappings;

public sealed class ComplianceMappingProfile : Profile
{
    private const string CreatedByParam = "createdBy";
    private const string UpdatedByParam = "updatedBy";

    public ComplianceMappingProfile()
    {
        CreateMap<User, AuditUserDto>()
            .ForCtorParam("userId", opt => opt.MapFrom(s => s.Id))
            .ForCtorParam("fullName", opt => opt.MapFrom(s => s.FullName))
            .ForCtorParam("email", opt => opt.MapFrom(s => s.Email));

        CreateMap<SocialInsuranceRateVersion, SocialInsuranceRateDto>()
            .ForCtorParam(CreatedByParam, opt => opt.MapFrom(s => s.CreatedByUser))
            .ForCtorParam(UpdatedByParam, opt => opt.MapFrom(s => s.UpdatedByUser));

        CreateMap<PitBracket, PitBracketDto>();
        CreateMap<PitRegulationVersion, PitRegulationDto>()
            .ForCtorParam("brackets", opt => opt.MapFrom(s => s.Brackets.OrderBy(b => b.Order)))
            .ForCtorParam(CreatedByParam, opt => opt.MapFrom(s => s.CreatedByUser))
            .ForCtorParam(UpdatedByParam, opt => opt.MapFrom(s => s.UpdatedByUser));

        CreateMap<MinimumWageRegion, MinimumWageRegionDto>();
        CreateMap<MinimumWageVersion, MinimumWageDto>()
            .ForCtorParam("regions", opt => opt.MapFrom(s => s.Regions))
            .ForCtorParam(CreatedByParam, opt => opt.MapFrom(s => s.CreatedByUser))
            .ForCtorParam(UpdatedByParam, opt => opt.MapFrom(s => s.UpdatedByUser));

        CreateMap<AllowanceExemptThresholdVersion, AllowanceExemptThresholdDto>()
            .ForCtorParam("allowanceTypeCode", opt => opt.MapFrom(s => s.AllowanceType != null ? s.AllowanceType.Code : string.Empty))
            .ForCtorParam("allowanceTypeName", opt => opt.MapFrom(s => s.AllowanceType != null ? s.AllowanceType.Name : string.Empty))
            .ForCtorParam(CreatedByParam, opt => opt.MapFrom(s => s.CreatedByUser))
            .ForCtorParam(UpdatedByParam, opt => opt.MapFrom(s => s.UpdatedByUser));

        CreateMap<LaborLawConstantsVersion, LaborLawConstantsDto>()
            .ForCtorParam(CreatedByParam, opt => opt.MapFrom(s => s.CreatedByUser))
            .ForCtorParam(UpdatedByParam, opt => opt.MapFrom(s => s.UpdatedByUser));

        CreateMap<LegalDocument, LegalDocumentDto>()
            .ForCtorParam(CreatedByParam, opt => opt.MapFrom(s => s.CreatedByUser))
            .ForCtorParam(UpdatedByParam, opt => opt.MapFrom(s => s.UpdatedByUser));

        CreateMap<LegalDocument, LegalDocumentSummaryDto>();
    }
}
