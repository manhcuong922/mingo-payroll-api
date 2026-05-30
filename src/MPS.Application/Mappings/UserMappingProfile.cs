using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Mappings;

public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForCtorParam(nameof(UserDto.RoleName), opt => opt.MapFrom(src => src.Role.Name));
    }
}
