using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Mappings;

public class ContractTemplateMappingProfile : Profile
{
    public ContractTemplateMappingProfile()
    {
        CreateMap<ContractTemplate, ContractTemplateDto>()
            .ForCtorParam(nameof(ContractTemplateDto.Kind),
                opt => opt.MapFrom(src => src.Kind.ToString()))
            .ForCtorParam(nameof(ContractTemplateDto.ContractCategory),
                opt => opt.MapFrom(src => src.ContractCategory.HasValue ? src.ContractCategory.Value.ToString() : null))
            .ForCtorParam(nameof(ContractTemplateDto.ContractSubtype),
                opt => opt.MapFrom(src => src.ContractSubtype.HasValue ? src.ContractSubtype.Value.ToString() : null))
            .ForCtorParam(nameof(ContractTemplateDto.FileName),
                opt => opt.MapFrom(src => src.TemplateFile != null ? src.TemplateFile.FileName : null));
    }
}
