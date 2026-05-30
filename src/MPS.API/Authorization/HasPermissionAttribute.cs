using Microsoft.AspNetCore.Authorization;

namespace MPS.API.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
            throw new ArgumentException("Permission code is required.", nameof(permission));

        Policy = $"{PermissionPolicyProvider.PolicyPrefix}{PermissionPolicyProvider.AllMarker}{permission}";
    }
}
