using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Role.Queries;

public sealed class RoleGetAllQueryHandler : IRequestHandler<RoleGetAllQuery, IReadOnlyList<RoleDto>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;

    public RoleGetAllQueryHandler(
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository)
    {
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
    }

    public async Task<IReadOnlyList<RoleDto>> Handle(RoleGetAllQuery request, CancellationToken cancellationToken)
    {
        var roles = await _roleRepository.GetAllAsync(cancellationToken);
        var result = new List<RoleDto>(roles.Count);

        foreach (var role in roles)
        {
            var perms = await _rolePermissionRepository.GetByRoleIdAsync(role.Id, cancellationToken);
            result.Add(new RoleDto(
                role.Id,
                role.Name,
                role.Description,
                role.IsSystem,
                role.CreatedAt,
                role.UpdatedAt,
                perms.Count));
        }

        return result;
    }
}
