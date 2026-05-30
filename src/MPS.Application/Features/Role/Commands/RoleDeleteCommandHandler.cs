using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Role.Commands;

public sealed class RoleDeleteCommandHandler : IRequestHandler<RoleDeleteCommand, bool>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IPermissionResolver _permissionResolver;

    public RoleDeleteCommandHandler(
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IPermissionResolver permissionResolver)
    {
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _permissionResolver = permissionResolver;
    }

    public async Task<bool> Handle(RoleDeleteCommand request, CancellationToken cancellationToken)
    {
        var existing = await _roleRepository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (existing.IsSystem)
            throw new SystemRoleModificationException("System roles cannot be deleted.");

        if (await _roleRepository.HasUsersAsync(request.Id, cancellationToken))
            throw new RoleInUseException("Cannot delete role: it is assigned to one or more users.");

        await _rolePermissionRepository.DeleteByRoleIdAsync(request.Id, cancellationToken);
        var deleted = await _roleRepository.DeleteAsync(request.Id, cancellationToken);

        _permissionResolver.InvalidateRoleCache(request.Id);
        return deleted;
    }
}
