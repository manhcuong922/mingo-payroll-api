using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Role.Commands;

public sealed class UpdateRolePermissionsCommandHandler : IRequestHandler<UpdateRolePermissionsCommand, bool>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IPermissionResolver _permissionResolver;

    public UpdateRolePermissionsCommandHandler(
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

    public async Task<bool> Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
        if (role is null) return false;

        var distinctIds = request.PermissionIds.Distinct().ToList();
        if (distinctIds.Count > 0)
        {
            var existing = await _permissionRepository.GetByIdsAsync(distinctIds, cancellationToken);
            if (existing.Count != distinctIds.Count)
                throw new DomainValidationException("One or more permission ids are invalid.");
        }

        await _rolePermissionRepository.ReplaceRolePermissionsAsync(request.RoleId, distinctIds, cancellationToken);
        _permissionResolver.InvalidateRoleCache(request.RoleId);
        return true;
    }
}
