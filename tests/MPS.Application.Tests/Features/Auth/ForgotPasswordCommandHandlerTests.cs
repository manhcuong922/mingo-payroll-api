using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Options;
using MPS.Application.Features.Auth.Commands;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.Auth;

public sealed class ForgotPasswordCommandHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordResetTokenRepository> _tokens = new();
    private readonly Mock<IEmailSender> _email = new();

    private static readonly PasswordResetOptions DefaultOptions = new()
    {
        TokenLifetimeHours = 1,
        FrontendBaseUrl = "https://operix.fun",
        LinkPath = "/reset-password?token={token}",
    };

    private ForgotPasswordCommandHandler CreateSut(PasswordResetOptions? options = null) =>
        new(_users.Object, _tokens.Object, _email.Object,
            Microsoft.Extensions.Options.Options.Create(options ?? DefaultOptions),
            NullLogger<ForgotPasswordCommandHandler>.Instance);

    private void SetupUsers(params MPS.Domain.Entities.User[] users) =>
        _users.Setup(u => u.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(users);

    [Fact]
    public async Task Handle_SendsMailWithClickableLink_ContainingIssuedToken()
    {
        // Arrange — mail phải chứa link bấm được, không bắt user tự ghép token vào URL.
        var user = UserFactory.CreateActive(email: "alice@mingo.local");
        SetupUsers(user);

        string? storedHash = null;
        _tokens.Setup(t => t.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
            .Callback<PasswordResetToken, CancellationToken>((t, _) => storedHash = t.TokenHash)
            .ReturnsAsync(1);

        string? sentToken = null;
        string? sentUrl = null;
        _email.Setup(e => e.SendPasswordResetAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, DateTime, CancellationToken>(
                (_, token, url, _, _) => { sentToken = token; sentUrl = url; })
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateSut().Handle(
            new ForgotPasswordCommand("alice@mingo.local", "1.2.3.4"), CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        sentUrl.Should().Be($"https://operix.fun/reset-password?token={Uri.EscapeDataString(sentToken!)}");
        storedHash.Should().NotBeNullOrEmpty();
        storedHash.Should().NotBe(sentToken, "DB chỉ lưu hash, không lưu token thô");
    }

    [Fact]
    public async Task Handle_FallsBackToPath_WhenFrontendBaseUrlNotConfigured()
    {
        SetupUsers(UserFactory.CreateActive(email: "alice@mingo.local"));

        string? sentUrl = null;
        _email.Setup(e => e.SendPasswordResetAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, DateTime, CancellationToken>((_, _, url, _, _) => sentUrl = url)
            .Returns(Task.CompletedTask);

        await CreateSut(new PasswordResetOptions { FrontendBaseUrl = null }).Handle(
            new ForgotPasswordCommand("alice@mingo.local", null), CancellationToken.None);

        sentUrl.Should().StartWith("/reset-password?token=");
    }

    [Fact]
    public async Task Handle_UsesConfiguredTokenLifetime()
    {
        SetupUsers(UserFactory.CreateActive(email: "alice@mingo.local"));

        DateTime? expiresAt = null;
        _email.Setup(e => e.SendPasswordResetAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, DateTime, CancellationToken>((_, _, _, exp, _) => expiresAt = exp)
            .Returns(Task.CompletedTask);

        var options = new PasswordResetOptions { TokenLifetimeHours = 3, FrontendBaseUrl = "https://operix.fun" };
        await CreateSut(options).Handle(
            new ForgotPasswordCommand("alice@mingo.local", null), CancellationToken.None);

        expiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(3), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Handle_InvalidatesPreviousTokens_BeforeIssuingNewOne()
    {
        var user = UserFactory.CreateActive(email: "alice@mingo.local");
        SetupUsers(user);

        await CreateSut().Handle(
            new ForgotPasswordCommand("alice@mingo.local", null), CancellationToken.None);

        _tokens.Verify(t => t.InvalidateActiveTokensForUserAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MatchesEmailCaseInsensitively()
    {
        SetupUsers(UserFactory.CreateActive(email: "alice@mingo.local"));

        await CreateSut().Handle(
            new ForgotPasswordCommand("ALICE@Mingo.Local", null), CancellationToken.None);

        _email.Verify(e => e.SendPasswordResetAsync(
            "alice@mingo.local", It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Không lộ việc email có tồn tại hay không: vẫn trả true, nhưng không gửi mail.</summary>
    [Fact]
    public async Task Handle_ReturnsTrueWithoutSendingMail_WhenEmailNotFound()
    {
        SetupUsers(UserFactory.CreateActive(email: "alice@mingo.local"));

        var result = await CreateSut().Handle(
            new ForgotPasswordCommand("nobody@mingo.local", null), CancellationToken.None);

        result.Should().BeTrue();
        _email.VerifyNoOtherCalls();
        _tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ReturnsTrueWithoutSendingMail_WhenUserDisabled()
    {
        SetupUsers(UserFactory.CreateActive(email: "alice@mingo.local", status: 0));

        var result = await CreateSut().Handle(
            new ForgotPasswordCommand("alice@mingo.local", null), CancellationToken.None);

        result.Should().BeTrue();
        _email.VerifyNoOtherCalls();
        _tokens.VerifyNoOtherCalls();
    }
}
