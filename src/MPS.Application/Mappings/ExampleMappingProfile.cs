using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Mappings;

/// <summary>
/// Ví dụ Mapping Profile - cấu hình ánh xạ giữa Entity và DTO.
/// </summary>
public class ExampleMappingProfile : Profile
{
    public ExampleMappingProfile()
    {
        CreateMap<ExampleEntity, ExampleDto>()
            .ConstructUsing(src => new ExampleDto(src.Id, src.Name, src.CreatedAt));
    }
}
