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
    public class UserMappingProfile : Profile
    {
        public UserMappingProfile()
        {
            CreateMap<User, UserDto>()
            .ForCtorParam(nameof(UserDto.RoleName), opt => opt.MapFrom(src => src.Role.Name));
        }
    }
}
