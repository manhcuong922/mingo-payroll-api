using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Mappings;

public class DepartmentMappingProfile : Profile
{
    public DepartmentMappingProfile()
    {
        CreateMap<Department, DepartmentDto>()
            .ForMember(d => d.ManagerName, o => o.MapFrom(s =>
                s.Manager != null ? s.Manager.FullName : null))
            .ForMember(d => d.EmployeeCount, o => o.MapFrom(s => s.Employees.Count));
    }
}
