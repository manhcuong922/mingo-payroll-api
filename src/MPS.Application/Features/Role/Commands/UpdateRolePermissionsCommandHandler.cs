using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Role.Commands;

public sealed class UpdateRolePermissionsCommandHandler : IRequestHandler<UpdateRolePermissionsCommand, bool>
{
    // SystemAdmin must always retain these permissions, otherwise the
    // last admin could accidentally lock everyone out of role management.
    private static readonly IReadOnlySet<int> SystemAdminRequiredPermissionIds = new HashSet<int>
    {
        PermissionIds.RoleReadAll,
        PermissionIds.RoleCreateAll,
        PermissionIds.RoleUpdateAll,
        PermissionIds.RoleDeleteAll,
        PermissionIds.RoleManagePermissionsAll,
        PermissionIds.RefreshTokenRevokeAll,
    };

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

        var distinctIds = request.PermissionIds.Distinct().ToHashSet();

        if (request.RoleId == SystemRoles.SystemAdminId)
        {
            var missing = SystemAdminRequiredPermissionIds.Where(id => !distinctIds.Contains(id)).ToList();
            if (missing.Count > 0)
                throw new SystemRoleModificationException(
                    $"SystemAdmin must retain its role-management permissions. Missing permission ids: {string.Join(", ", missing)}.");
        }

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
