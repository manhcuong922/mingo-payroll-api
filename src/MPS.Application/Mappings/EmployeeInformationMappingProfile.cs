using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Mappings;

public class EmployeeInformationMappingProfile : Profile
{
    public EmployeeInformationMappingProfile()
    {
        CreateMap<EmployeeInformation, EmployeeInformationDto>()
            .ForCtorParam(nameof(EmployeeInformationDto.EmployeeCode),
                opt => opt.MapFrom(src => src.User != null ? src.User.EmployeeCode : null))
            .ForCtorParam(nameof(EmployeeInformationDto.Gender),
                opt => opt.MapFrom(src => src.Gender.ToString()))
            .ForCtorParam(nameof(EmployeeInformationDto.PositionName),
                opt => opt.MapFrom(src => src.Position.Name))
            .ForCtorParam(nameof(EmployeeInformationDto.DepartmentId),
                opt => opt.MapFrom(src => src.DepartmentId))
            .ForCtorParam(nameof(EmployeeInformationDto.DepartmentName),
                opt => opt.MapFrom(src => src.Department != null ? src.Department.Name : null));
    }
}
