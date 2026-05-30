using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace MPS.API.Authorization;

public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    public const string PolicyPrefix = "Permission:";
    public const string AllMarker = "All:";
    public const string AnyMarker = "Any:";

    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal))
            return _fallback.GetPolicyAsync(policyName);

        var body = policyName[PolicyPrefix.Length..];
        bool requireAll;

        if (body.StartsWith(AllMarker, StringComparison.Ordinal))
        {
            requireAll = true;
            body = body[AllMarker.Length..];
        }
        else if (body.StartsWith(AnyMarker, StringComparison.Ordinal))
        {
            requireAll = false;
            body = body[AnyMarker.Length..];
        }
        else
        {
            // Default to Any semantics if marker missing
            requireAll = false;
        }

        var permissions = body
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var policy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permissions, requireAll))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
