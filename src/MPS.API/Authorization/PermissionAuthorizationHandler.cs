using Microsoft.AspNetCore.Authorization;
using MPS.Infrastructure.Security;

namespace MPS.API.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        if (requirement.RequiredPermissions.Count == 0)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var userPermissions = context.User
            .FindAll(JwtTokenService.PermissionsClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

        var matches = requirement.RequireAll
            ? requirement.RequiredPermissions.All(userPermissions.Contains)
            : requirement.RequiredPermissions.Any(userPermissions.Contains);

        if (matches)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
