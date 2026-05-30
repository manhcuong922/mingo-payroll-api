using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Auth.Commands;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.Auth;

public sealed class ChangePasswordCommandHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IRefreshTokenService> _refresh = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private ChangePasswordCommandHandler CreateSut() =>
        new(_users.Object, _hasher.Object, _refresh.Object, _currentUser.Object);

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenNotAuthenticated()
    {
        _currentUser.SetupGet(c => c.UserId).Returns((int?)null);

        var act = () => CreateSut().Handle(new ChangePasswordCommand("old", "newpass1", null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("Authentication required.");
    }

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenOldPasswordWrong()
    {
        var user = UserFactory.CreateActive(passwordHash: "stored");
        _currentUser.SetupGet(c => c.UserId).Returns(user.Id);
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("wrong", "stored")).Returns(false);

        var act = () => CreateSut().Handle(new ChangePasswordCommand("wrong", "newpass1", null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("Current password is incorrect.");
    }

    [Fact]
    public async Task Handle_RevokesAllSessions_OnSuccess()
    {
        var user = UserFactory.CreateActive(passwordHash: "stored");
        _currentUser.SetupGet(c => c.UserId).Returns(user.Id);
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("old", "stored")).Returns(true);
        _hasher.Setup(h => h.Hash("newpass1")).Returns("new-hash");
        _users.Setup(r => r.UpdatePasswordAsync(user.Id, "new-hash", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var ok = await CreateSut().Handle(new ChangePasswordCommand("old", "newpass1", "1.1.1.1"), CancellationToken.None);

        ok.Should().BeTrue();
        _refresh.Verify(r => r.RevokeAllForUserAsync(user.Id, "Password changed", "1.1.1.1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
