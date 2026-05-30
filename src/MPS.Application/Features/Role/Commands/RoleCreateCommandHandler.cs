using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Role.Commands;

public sealed class RoleCreateCommandHandler : IRequestHandler<RoleCreateCommand, int>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IPermissionResolver _permissionResolver;

    public RoleCreateCommandHandler(
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IPermissionRepository permissionRepository,
        IPermissionResolver permissionResolver)
    {
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _permissionRepository = permissionRepository;
        _permissionResolver = permissionResolver;
    }

    public async Task<int> Handle(RoleCreateCommand request, CancellationToken cancellationToken)
    {
        var distinctPermissionIds = request.PermissionIds.Distinct().ToList();
        if (distinctPermissionIds.Count > 0)
        {
            var existing = await _permissionRepository.GetByIdsAsync(distinctPermissionIds, cancellationToken);
            if (existing.Count != distinctPermissionIds.Count)
                throw new DomainValidationException("One or more permission ids are invalid.");
        }

        var role = new Domain.Entities.Role
        {
            Name = request.Name,
            Description = request.Description,
            IsSystem = false,
            CreatedAt = DateTime.UtcNow,
        };

        var roleId = await _roleRepository.AddAsync(role, cancellationToken);

        if (distinctPermissionIds.Count > 0)
            await _rolePermissionRepository.ReplaceRolePermissionsAsync(roleId, distinctPermissionIds, cancellationToken);

        _permissionResolver.InvalidateRoleCache(roleId);
        return roleId;
    }
}
