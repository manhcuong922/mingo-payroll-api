using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Application.Features.Auth.Commands;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Exceptions;
using Xunit;

namespace MPS.Application.Tests.Features.Auth;

public sealed class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IRefreshTokenService> _refresh = new();
    private readonly Mock<IJwtTokenService> _jwt = new();
    private readonly Mock<IPermissionResolver> _perms = new();

    private RefreshTokenCommandHandler CreateSut() =>
        new(_refresh.Object, _jwt.Object, _perms.Object);

    [Fact]
    public async Task Handle_ReturnsRotatedTokens_OnSuccess()
    {
        var user = UserFactory.CreateActive();
        var newRefresh = new RefreshTokenResult("new-refresh", DateTime.UtcNow.AddDays(7));
        var access = new AccessTokenResult("new-jwt", DateTime.UtcNow.AddMinutes(60), "jti");
        var permissions = new[] { "User.Read.Self" };

        _refresh.Setup(r => r.RotateAsync("old", "1.1.1.1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenRotationResult(user, newRefresh));
        _perms.Setup(p => p.GetPermissionsForRoleAsync(user.RoleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);
        _jwt.Setup(j => j.GenerateAccessToken(user, permissions)).Returns(access);

        var result = await CreateSut().Handle(new RefreshTokenCommand("old", "1.1.1.1"), CancellationToken.None);

        result.AccessToken.Should().Be(access);
        result.RefreshToken.Should().Be(newRefresh);
    }

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenUserDisabled()
    {
        var user = UserFactory.CreateActive(status: 0);
        var newRefresh = new RefreshTokenResult("new", DateTime.UtcNow.AddDays(7));

        _refresh.Setup(r => r.RotateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenRotationResult(user, newRefresh));

        var act = () => CreateSut().Handle(new RefreshTokenCommand("token", null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("User account is disabled.");
    }
}
