using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Mappings;

public class TypePerformanceMetricMappingProfile : Profile
{
    public TypePerformanceMetricMappingProfile()
    {
        CreateMap<TypePerformanceMetric, TypePerformanceMetricDto>();
    }
}
