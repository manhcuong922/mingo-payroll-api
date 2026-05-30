using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Auth.Commands;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Entities;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.Auth;

public sealed class ResetPasswordCommandHandlerTests
{
    private readonly Mock<IPasswordResetTokenRepository> _tokens = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IRefreshTokenService> _refresh = new();

    private ResetPasswordCommandHandler CreateSut() =>
        new(_tokens.Object, _users.Object, _hasher.Object, _refresh.Object);

    private static string HashToken(string raw)
    {
        var bytes = Encoding.UTF8.GetBytes(raw);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenTokenMissing()
    {
        _tokens.Setup(t => t.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PasswordResetToken?)null);

        var act = () => CreateSut().Handle(new ResetPasswordCommand("raw", "newpass1", null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("Reset token is invalid.");
    }

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenTokenAlreadyUsed()
    {
        var rawToken = "raw";
        _tokens.Setup(t => t.GetByTokenHashAsync(HashToken(rawToken), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordResetToken
            {
                Id = 1,
                UserId = 1,
                TokenHash = HashToken(rawToken),
                UsedAt = DateTime.UtcNow.AddMinutes(-5),
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            });

        var act = () => CreateSut().Handle(new ResetPasswordCommand(rawToken, "newpass1", null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("Reset token has already been used.");
    }

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenTokenExpired()
    {
        var rawToken = "raw";
        _tokens.Setup(t => t.GetByTokenHashAsync(HashToken(rawToken), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordResetToken
            {
                Id = 1,
                UserId = 1,
                TokenHash = HashToken(rawToken),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            });

        var act = () => CreateSut().Handle(new ResetPasswordCommand(rawToken, "newpass1", null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("Reset token has expired.");
    }

    [Fact]
    public async Task Handle_ResetsPassword_RevokesAllTokens_OnSuccess()
    {
        var rawToken = "raw";
        var user = UserFactory.CreateActive();
        _tokens.Setup(t => t.GetByTokenHashAsync(HashToken(rawToken), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordResetToken
            {
                Id = 99,
                UserId = user.Id,
                TokenHash = HashToken(rawToken),
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                User = user,
            });
        _hasher.Setup(h => h.Hash("newpass1")).Returns("new-hash");
        _users.Setup(u => u.UpdatePasswordAsync(user.Id, "new-hash", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var ok = await CreateSut().Handle(new ResetPasswordCommand(rawToken, "newpass1", "5.5.5.5"), CancellationToken.None);

        ok.Should().BeTrue();
        _tokens.Verify(t => t.MarkUsedAsync(99, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _refresh.Verify(r => r.RevokeAllForUserAsync(user.Id, "Password reset", "5.5.5.5", It.IsAny<CancellationToken>()), Times.Once);
    }
}
