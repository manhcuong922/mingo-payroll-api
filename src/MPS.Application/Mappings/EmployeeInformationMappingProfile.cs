using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Mappings
{
    public class EmployeeInformationMappingProfile : Profile
    {
        public EmployeeInformationMappingProfile()
        {
            CreateMap<EmployeeInformation, EmployeeInformationDto>()
                .ForCtorParam(nameof(EmployeeInformationDto.PositionName), opt => opt.MapFrom(src => src.Position.Name));
        }
    }
}
