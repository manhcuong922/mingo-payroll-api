using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Mappings;

public class ContractTerminationMappingProfile : Profile
{
    public ContractTerminationMappingProfile()
    {
        CreateMap<ContractTermination, ContractTerminationDto>()
            .ForCtorParam(nameof(ContractTerminationDto.TerminationParty),
                opt => opt.MapFrom(src => src.TerminationParty.ToString()))
            .ForCtorParam(nameof(ContractTerminationDto.ReasonCode),
                opt => opt.MapFrom(src => src.ReasonRef != null ? src.ReasonRef.Code : string.Empty))
            .ForCtorParam(nameof(ContractTerminationDto.ReasonDescription),
                opt => opt.MapFrom(src => src.ReasonRef != null ? src.ReasonRef.Description : string.Empty));
    }
}
