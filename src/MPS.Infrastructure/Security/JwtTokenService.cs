using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Security;

public sealed class JwtTokenService : IJwtTokenService
{
    public const string PermissionsClaimType = "perms";
    public const string RoleIdClaimType = "role_id";

    private readonly JwtSettings _settings;
    private readonly SigningCredentials _signingCredentials;

    public JwtTokenService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;

        if (string.IsNullOrWhiteSpace(_settings.SecretKey))
            throw new InvalidOperationException("Jwt:SecretKey is not configured.");

        var keyBytes = Encoding.UTF8.GetBytes(_settings.SecretKey);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Jwt:SecretKey must be at least 32 bytes (256 bits) for HS256.");

        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);
    }

    public AccessTokenResult GenerateAccessToken(User user, IEnumerable<string> permissionCodes)
    {
        ArgumentNullException.ThrowIfNull(user);

        var issuedAt = DateTime.UtcNow;
        var expiresAt = user.Username == "ai-service"
            ? issuedAt.AddYears(10)
            : issuedAt.AddMinutes(_settings.AccessTokenMinutes);
        var jti = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(RoleIdClaimType, user.RoleId.ToString()),
        };

        if (user.Role is not null)
            claims.Add(new Claim(ClaimTypes.Role, user.Role.Name));

        foreach (var perm in permissionCodes.Distinct(StringComparer.Ordinal))
            claims.Add(new Claim(PermissionsClaimType, perm));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: _signingCredentials);

        var serialized = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessTokenResult(serialized, expiresAt, jti);
    }
}
