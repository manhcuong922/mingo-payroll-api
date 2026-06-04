using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Mappings;

public class ContractPartyMappingProfile : Profile
{
    public ContractPartyMappingProfile()
    {
        CreateMap<ContractParty, ContractPartyDto>();
    }
}
