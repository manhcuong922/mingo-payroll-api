using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Auth.Commands;
using Xunit;

namespace MPS.Application.Tests.Features.Auth;

public sealed class LogoutCommandHandlerTests
{
    [Fact]
    public async Task Handle_RevokesToken_WithLogoutReason()
    {
        var refresh = new Mock<IRefreshTokenService>();
        refresh.Setup(r => r.RevokeAsync("token", "User logout", "1.1.1.1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new LogoutCommandHandler(refresh.Object);

        var result = await sut.Handle(new LogoutCommand("token", "1.1.1.1"), CancellationToken.None);

        result.Should().BeTrue();
        refresh.VerifyAll();
    }
}
