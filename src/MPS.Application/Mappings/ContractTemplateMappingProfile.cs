using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Mappings;

public class ContractTemplateMappingProfile : Profile
{
    public ContractTemplateMappingProfile()
    {
        CreateMap<ContractTemplate, ContractTemplateDto>()
            .ForCtorParam(nameof(ContractTemplateDto.ContractCategory),
                opt => opt.MapFrom(src => src.ContractCategory.ToString()))
            .ForCtorParam(nameof(ContractTemplateDto.ContractSubtype),
                opt => opt.MapFrom(src => src.ContractSubtype.ToString()))
            .ForCtorParam(nameof(ContractTemplateDto.FileName),
                opt => opt.MapFrom(src => src.TemplateFile != null ? src.TemplateFile.FileName : null));
    }
}
