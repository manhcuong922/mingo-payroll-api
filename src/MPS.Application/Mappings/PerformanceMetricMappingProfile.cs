using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Mappings;

/// <summary>
/// Mapping Profile cho PerformanceMetric & Role.
/// </summary>
public class PerformanceMetricMappingProfile : Profile
{
    public PerformanceMetricMappingProfile()
    {
        CreateMap<Role, RoleDto>();
        CreateMap<PerformanceMetric, PerformanceMetricDto>();
    }
}
