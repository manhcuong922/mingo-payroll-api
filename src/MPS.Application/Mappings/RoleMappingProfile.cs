using AutoMapper;
using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Mappings;

public sealed class RoleMappingProfile : Profile
{
    public RoleMappingProfile()
    {
        CreateMap<Role, RoleDto>()
            .ForCtorParam(nameof(RoleDto.PermissionCount),
                opt => opt.MapFrom(src => src.RolePermissions.Count));

        CreateMap<Role, RoleDetailDto>()
            .ForCtorParam(nameof(RoleDetailDto.Permissions),
                opt => opt.MapFrom(src =>
                    src.RolePermissions.Select(rp => rp.Permission).ToList()));
    }
}
