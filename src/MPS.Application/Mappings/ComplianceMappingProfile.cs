using AutoMapper;
using MPS.Application.DTOs.Compliance;
using MPS.Domain.Entities.Compliance;

namespace MPS.Application.Mappings;

public sealed class ComplianceMappingProfile : Profile
{
    public ComplianceMappingProfile()
    {
        CreateMap<SocialInsuranceRateVersion, SocialInsuranceRateDto>();

        CreateMap<PitBracket, PitBracketDto>();
        CreateMap<PitRegulationVersion, PitRegulationDto>()
            .ForMember(d => d.Brackets, opt => opt.MapFrom(s => s.Brackets.OrderBy(b => b.Order)));

        CreateMap<OtRateEntry, OtRateEntryDto>();
        CreateMap<OtRateVersion, OtRateDto>()
            .ForMember(d => d.Entries, opt => opt.MapFrom(s => s.Entries));

        CreateMap<MinimumWageRegion, MinimumWageRegionDto>();
        CreateMap<MinimumWageVersion, MinimumWageDto>()
            .ForMember(d => d.Regions, opt => opt.MapFrom(s => s.Regions));

        CreateMap<AllowanceExemptThresholdVersion, AllowanceExemptThresholdDto>()
            .ForMember(d => d.AllowanceTypeCode, opt => opt.MapFrom(s => s.AllowanceType != null ? s.AllowanceType.Code : string.Empty))
            .ForMember(d => d.AllowanceTypeName, opt => opt.MapFrom(s => s.AllowanceType != null ? s.AllowanceType.Name : string.Empty));

        CreateMap<LaborLawConstantsVersion, LaborLawConstantsDto>();
    }
}
