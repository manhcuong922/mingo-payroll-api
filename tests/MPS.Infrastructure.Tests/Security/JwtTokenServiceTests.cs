using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MPS.Domain.Entities;
using MPS.Infrastructure.Security;
using Xunit;

namespace MPS.Infrastructure.Tests.Security;

public sealed class JwtTokenServiceTests
{
    private static JwtSettings Settings() => new()
    {
        Issuer = "test-issuer",
        Audience = "test-aud",
        SecretKey = "TEST_SECRET_KEY_AT_LEAST_32_BYTES_LONG!!!!!!",
        AccessTokenMinutes = 30,
        RefreshTokenDays = 1,
        ClockSkewSeconds = 0,
    };

    private static User TestUser() => new()
    {
        Id = 42,
        Username = "alice",
        Email = "alice@test",
        RoleId = 7,
        Role = new Role { Id = 7, Name = "Employee", IsSystem = true, CreatedAt = DateTime.UtcNow },
    };

    [Fact]
    public void GenerateAccessToken_ProducesValidJwt_WithExpectedClaims()
    {
        var sut = new JwtTokenService(Options.Create(Settings()));
        var perms = new[] { "User.Read.Self", "EmployeeInformation.Read.Self" };

        var result = sut.GenerateAccessToken(TestUser(), perms);

        result.Token.Should().NotBeNullOrEmpty();
        result.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromMinutes(1));
        result.Jti.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.Token);

        token.Issuer.Should().Be("test-issuer");
        token.Audiences.Should().Contain("test-aud");
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == "42");
        token.Claims.Should().Contain(c => c.Type == JwtTokenService.RoleIdClaimType && c.Value == "7");
        token.Claims.Count(c => c.Type == JwtTokenService.PermissionsClaimType).Should().Be(2);
    }

    [Fact]
    public void Constructor_Throws_WhenSecretTooShort()
    {
        var settings = Settings();
        var act = () => new JwtTokenService(Options.Create(new JwtSettings
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            SecretKey = "short",
            AccessTokenMinutes = 1,
            RefreshTokenDays = 1,
            ClockSkewSeconds = 0,
        }));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GenerateAccessToken_DeduplicatesDuplicatePermissions()
    {
        var sut = new JwtTokenService(Options.Create(Settings()));

        var result = sut.GenerateAccessToken(TestUser(), new[] { "User.Read.Self", "User.Read.Self" });

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        token.Claims.Count(c => c.Type == JwtTokenService.PermissionsClaimType).Should().Be(1);
    }
}
