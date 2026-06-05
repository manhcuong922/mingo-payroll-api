using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Mappings;

public class ContractAppendixMappingProfile : Profile
{
    public ContractAppendixMappingProfile()
    {
        CreateMap<ContractAppendix, ContractAppendixDto>()
            .ForCtorParam(nameof(ContractAppendixDto.AppendixType),
                opt => opt.MapFrom(src => src.AppendixType.ToString()))
            .ForCtorParam(nameof(ContractAppendixDto.Status),
                opt => opt.MapFrom(src => src.Status.ToString()));
    }
}
