using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using MPS.Domain.Entities;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Security;
using Xunit;

namespace MPS.Infrastructure.Tests.Security;

public sealed class RefreshTokenServiceTests
{
    private readonly Mock<IRefreshTokenRepository> _repo = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly JwtSettings _settings = new()
    {
        Issuer = "iss", Audience = "aud", SecretKey = new string('x', 64),
        AccessTokenMinutes = 60, RefreshTokenDays = 7, ClockSkewSeconds = 0,
    };

    private RefreshTokenService CreateSut() => new(_repo.Object, _users.Object, Options.Create(_settings));

    [Fact]
    public async Task IssueAsync_CreatesTokenAndPersists()
    {
        var user = new User { Id = 1 };

        var result = await CreateSut().IssueAsync(user, "ip", CancellationToken.None);

        result.Token.Should().NotBeNullOrEmpty();
        result.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(1));
        _repo.Verify(r => r.AddAsync(It.Is<RefreshToken>(t =>
            t.UserId == 1 && t.CreatedByIp == "ip" && !string.IsNullOrEmpty(t.TokenHash)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotateAsync_Throws_WhenTokenMissing()
    {
        _repo.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        var act = () => CreateSut().RotateAsync("missing", null, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("Refresh token is invalid.");
    }

    [Fact]
    public async Task RotateAsync_RevokesEntireChain_WhenReusedTokenPresented()
    {
        var stored = new RefreshToken
        {
            Id = 1,
            UserId = 42,
            RevokedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
        };
        _repo.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

        var act = () => CreateSut().RotateAsync("reused", "1.1.1.1", CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("Refresh token reuse detected. All sessions revoked.");
        _repo.Verify(r => r.RevokeAllForUserAsync(42, It.IsAny<string>(), "1.1.1.1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotateAsync_Throws_WhenExpired()
    {
        var stored = new RefreshToken
        {
            Id = 1,
            UserId = 42,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        };
        _repo.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

        var act = () => CreateSut().RotateAsync("token", null, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("Refresh token has expired.");
    }

    [Fact]
    public async Task RotateAsync_IssuesNewToken_AndRevokesOld_OnSuccess()
    {
        var user = new User { Id = 42 };
        var stored = new RefreshToken
        {
            Id = 1,
            UserId = 42,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = user,
        };
        _repo.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

        var result = await CreateSut().RotateAsync("old", "9.9.9.9", CancellationToken.None);

        result.User.Should().BeSameAs(user);
        result.NewRefreshToken.Token.Should().NotBeNullOrEmpty();
        _repo.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.UpdateAsync(It.Is<RefreshToken>(t => t.Id == 1 && t.RevokedAt != null), It.IsAny<CancellationToken>()), Times.Once);
    }
}
