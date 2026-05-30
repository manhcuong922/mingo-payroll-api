using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Mappings;

public class EmployeeInformationMappingProfile : Profile
{
    public EmployeeInformationMappingProfile()
    {
        CreateMap<EmployeeInformation, EmployeeInformationDto>()
            .ForCtorParam(nameof(EmployeeInformationDto.EmployeeCode), opt => opt.MapFrom(src => src.User.EmployeeCode))
            .ForCtorParam(nameof(EmployeeInformationDto.FullName), opt => opt.MapFrom(src => src.User.FullName))
            .ForCtorParam(nameof(EmployeeInformationDto.PositionName), opt => opt.MapFrom(src => src.Position.Name));
    }
}
