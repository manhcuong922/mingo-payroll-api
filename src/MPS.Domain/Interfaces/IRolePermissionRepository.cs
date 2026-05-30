using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IRolePermissionRepository
{
    Task<IReadOnlyList<RolePermission>> GetByRoleIdAsync(int roleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetPermissionCodesByRoleIdAsync(int roleId, CancellationToken cancellationToken = default);
    Task ReplaceRolePermissionsAsync(int roleId, IEnumerable<int> permissionIds, CancellationToken cancellationToken = default);
    Task DeleteByRoleIdAsync(int roleId, CancellationToken cancellationToken = default);
}
