using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MPS.Application.Common.Interfaces;
using MPS.Infrastructure.Security;

namespace MPS.API.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public int? UserId
    {
        get
        {
            var raw = Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Username =>
        Principal?.FindFirstValue(ClaimTypes.Name)
            ?? Principal?.FindFirstValue(JwtRegisteredClaimNames.UniqueName);

    public int? RoleId
    {
        get
        {
            var raw = Principal?.FindFirstValue(JwtTokenService.RoleIdClaimType);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? RoleName => Principal?.FindFirstValue(ClaimTypes.Role);

    public IReadOnlyCollection<string> Permissions =>
        Principal?
            .FindAll(JwtTokenService.PermissionsClaimType)
            .Select(c => c.Value)
            .ToArray()
        ?? Array.Empty<string>();

    public bool HasPermission(string permissionCode)
    {
        if (string.IsNullOrEmpty(permissionCode)) return false;
        return Principal?
            .FindAll(JwtTokenService.PermissionsClaimType)
            .Any(c => string.Equals(c.Value, permissionCode, StringComparison.Ordinal)) ?? false;
    }
}
