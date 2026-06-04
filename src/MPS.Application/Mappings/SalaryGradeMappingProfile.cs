using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Mappings;

public class SalaryGradeMappingProfile : Profile
{
    public SalaryGradeMappingProfile()
    {
        CreateMap<SalaryGrade, SalaryGradeDto>();
    }
}
