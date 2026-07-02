using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Mappings;

public class DepartmentMappingProfile : Profile
{
    public DepartmentMappingProfile()
    {
        // DepartmentDto là positional record → map qua ForCtorParam (ForMember không set được
        // ctor param, gây "needs a constructor with 0 args"). Các param còn lại (Id, Code, Name,
        // Description, ManagerId, IsActive) tự khớp theo tên.
        CreateMap<Department, DepartmentDto>()
            .ForCtorParam(nameof(DepartmentDto.ManagerName), o => o.MapFrom(s =>
                s.Manager != null ? s.Manager.FullName : null))
            .ForCtorParam(nameof(DepartmentDto.EmployeeCount), o => o.MapFrom(s => s.Employees.Count));
    }
}
