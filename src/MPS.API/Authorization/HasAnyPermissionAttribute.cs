using Microsoft.AspNetCore.Authorization;

namespace MPS.API.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HasAnyPermissionAttribute : AuthorizeAttribute
{
    public HasAnyPermissionAttribute(params string[] permissions)
    {
        if (permissions is null || permissions.Length == 0)
            throw new ArgumentException("At least one permission code is required.", nameof(permissions));

        Policy = $"{PermissionPolicyProvider.PolicyPrefix}{PermissionPolicyProvider.AnyMarker}{string.Join(',', permissions)}";
    }
}
