using Microsoft.AspNetCore.Authorization;

namespace MPS.API.Authorization;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public IReadOnlyList<string> RequiredPermissions { get; }
    public bool RequireAll { get; }

    public PermissionRequirement(IEnumerable<string> requiredPermissions, bool requireAll)
    {
        RequiredPermissions = requiredPermissions
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        RequireAll = requireAll;
    }
}
