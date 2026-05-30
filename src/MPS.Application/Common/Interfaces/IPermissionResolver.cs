namespace MPS.Application.Common.Interfaces;

public interface IPermissionResolver
{
    Task<IReadOnlyCollection<string>> GetPermissionsForRoleAsync(int roleId, CancellationToken cancellationToken = default);

    void InvalidateRoleCache(int roleId);
}
