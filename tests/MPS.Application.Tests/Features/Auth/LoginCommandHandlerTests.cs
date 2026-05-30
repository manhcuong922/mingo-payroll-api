using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Application.Features.Auth.Commands;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.Auth;

public sealed class LoginCommandHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IJwtTokenService> _jwt = new();
    private readonly Mock<IRefreshTokenService> _refresh = new();
    private readonly Mock<IPermissionResolver> _perms = new();

    private LoginCommandHandler CreateSut() =>
        new(_users.Object, _hasher.Object, _jwt.Object, _refresh.Object, _perms.Object);

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenUserNotFound()
    {
        _users.Setup(r => r.GetByUsernameAsync("ghost", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MPS.Domain.Entities.User?)null);

        var act = () => CreateSut().Handle(new LoginCommand("ghost", "pwd", null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("Invalid username or password.");
    }

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenPasswordInvalid()
    {
        var user = UserFactory.CreateActive(passwordHash: "stored-hash");
        _users.Setup(r => r.GetByUsernameAsync(user.Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("wrong", "stored-hash")).Returns(false);

        var act = () => CreateSut().Handle(new LoginCommand(user.Username, "wrong", null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("Invalid username or password.");
    }

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenUserDisabled()
    {
        var user = UserFactory.CreateActive(status: 0, passwordHash: "stored-hash");
        _users.Setup(r => r.GetByUsernameAsync(user.Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("pwd", "stored-hash")).Returns(true);

        var act = () => CreateSut().Handle(new LoginCommand(user.Username, "pwd", null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("User account is disabled.");
    }

    [Fact]
    public async Task Handle_ReturnsTokens_OnSuccess()
    {
        var user = UserFactory.CreateActive(passwordHash: "stored-hash");
        var permissions = new[] { "User.Read.Self" };
        var access = new AccessTokenResult("jwt-token", DateTime.UtcNow.AddMinutes(60), "jti");
        var refresh = new RefreshTokenResult("refresh-token", DateTime.UtcNow.AddDays(7));

        _users.Setup(r => r.GetByUsernameAsync(user.Username, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("pwd", "stored-hash")).Returns(true);
        _perms.Setup(p => p.GetPermissionsForRoleAsync(user.RoleId, It.IsAny<CancellationToken>())).ReturnsAsync(permissions);
        _jwt.Setup(j => j.GenerateAccessToken(user, permissions)).Returns(access);
        _refresh.Setup(r => r.IssueAsync(user, "1.2.3.4", It.IsAny<CancellationToken>())).ReturnsAsync(refresh);

        var result = await CreateSut().Handle(new LoginCommand(user.Username, "pwd", "1.2.3.4"), CancellationToken.None);

        result.AccessToken.Should().Be(access);
        result.RefreshToken.Should().Be(refresh);
        _users.Verify(r => r.UpdateLastLoginAsync(user.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
