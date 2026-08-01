using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Options;
using MPS.Application.Common.Services;
using MPS.Domain.Entities;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Common.Services;

/// <summary>
/// Plan §2 N8 (Hướng B). Verify <see cref="FirstLoginInvitationService"/>:
///   • Invalidate token cũ trước khi tạo token mới (không cho tồn tại song song).
///   • Tạo <see cref="PasswordResetToken"/> hạn = <c>FirstLoginOptions.TokenLifetimeHours</c>.
///   • Gửi email chứa link đặt password (KHÔNG password thô).
///   • Throw <see cref="NotFoundException"/> khi user không tồn tại.
///   • Throw <see cref="DomainValidationException"/> khi user không có email.
///   • Link được build đúng từ <c>FrontendBaseUrl</c> + <c>LinkPath</c>.
/// </summary>
public sealed class FirstLoginInvitationServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordResetTokenRepository> _tokens = new();
    private readonly Mock<IEmailSender> _email = new();

    private FirstLoginInvitationService CreateSut(FirstLoginOptions? options = null) =>
        new(_users.Object, _tokens.Object, _email.Object,
            Microsoft.Extensions.Options.Options.Create(options ?? new FirstLoginOptions
            {
                TokenLifetimeHours = 72,
                FrontendBaseUrl = "https://mps.local",
                LinkPath = "/first-login?token={token}",
            }));

    private static User BuildUser(int id = 42, string email = "user@company.vn", string username = "nva") =>
        new()
        {
            Id = id,
            FullName = "Nguyen Van A",
            Username = username,
            Email = email,
            Status = 1,
        };

    [Fact]
    public async Task IssueAsync_HappyPath_InvalidatesOldTokens_CreatesNewToken_AndSendsEmail()
    {
        _users.Setup(u => u.GetByIdAsync(42, It.IsAny<CancellationToken>()))
              .ReturnsAsync(BuildUser());

        var before = DateTime.UtcNow;
        var expiresAt = await CreateSut().IssueAsync(42);

        // Invalidate được gọi TRƯỚC khi tạo token mới.
        _tokens.Verify(t => t.InvalidateActiveTokensForUserAsync(42, It.IsAny<CancellationToken>()), Times.Once);
        _tokens.Verify(t => t.AddAsync(
                It.Is<PasswordResetToken>(x =>
                    x.UserId == 42
                    && !string.IsNullOrEmpty(x.TokenHash)
                    && x.ExpiresAt > x.CreatedAt),
                It.IsAny<CancellationToken>()),
            Times.Once);

        expiresAt.Should().BeAfter(before.AddHours(71)).And.BeBefore(before.AddHours(73));

        _email.Verify(s => s.SendFirstLoginInvitationAsync(
                "user@company.vn",
                "Nguyen Van A",
                "nva",
                It.Is<string>(url => url.StartsWith("https://mps.local/first-login?token=")),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IssueAsync_UsesLifetimeFromOptions()
    {
        _users.Setup(u => u.GetByIdAsync(42, It.IsAny<CancellationToken>()))
              .ReturnsAsync(BuildUser());

        var before = DateTime.UtcNow;
        var expiresAt = await CreateSut(new FirstLoginOptions { TokenLifetimeHours = 24 }).IssueAsync(42);

        expiresAt.Should().BeAfter(before.AddHours(23)).And.BeBefore(before.AddHours(25));
    }

    [Fact]
    public async Task IssueAsync_TokenValueIsUnique_AcrossInvocations()
    {
        _users.Setup(u => u.GetByIdAsync(42, It.IsAny<CancellationToken>()))
              .ReturnsAsync(BuildUser());

        var urls = new List<string>();
        _email.Setup(s => s.SendFirstLoginInvitationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, DateTime, CancellationToken>(
                (_, _, _, url, _, _) => urls.Add(url))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.IssueAsync(42);
        await sut.IssueAsync(42);

        urls.Should().HaveCount(2);
        urls[0].Should().NotBe(urls[1], "mỗi lần cấp token phải cho token khác nhau");
    }

    [Fact]
    public async Task IssueAsync_Throws_WhenUserNotFound()
    {
        _users.Setup(u => u.GetByIdAsync(999, It.IsAny<CancellationToken>()))
              .ReturnsAsync((User?)null);

        var act = () => CreateSut().IssueAsync(999);

        await act.Should().ThrowAsync<NotFoundException>();
        _tokens.VerifyNoOtherCalls();
        _email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task IssueAsync_Throws_WhenUserHasNoEmail()
    {
        _users.Setup(u => u.GetByIdAsync(42, It.IsAny<CancellationToken>()))
              .ReturnsAsync(BuildUser(email: ""));

        var act = () => CreateSut().IssueAsync(42);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*USER_EMAIL_MISSING*");
    }

    [Fact]
    public async Task IssueAsync_BuildsUrlFromFrontendBaseUrlAndLinkPath()
    {
        _users.Setup(u => u.GetByIdAsync(42, It.IsAny<CancellationToken>()))
              .ReturnsAsync(BuildUser());

        string? capturedUrl = null;
        _email.Setup(s => s.SendFirstLoginInvitationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, DateTime, CancellationToken>(
                (_, _, _, url, _, _) => capturedUrl = url)
            .Returns(Task.CompletedTask);

        await CreateSut(new FirstLoginOptions
        {
            TokenLifetimeHours = 72,
            FrontendBaseUrl = "https://mps.example.com/",  // trailing slash phải bị strip
            LinkPath = "/setup-account?t={token}",
        }).IssueAsync(42);

        capturedUrl.Should().NotBeNull();
        capturedUrl!.Should().StartWith("https://mps.example.com/setup-account?t=");
    }

    [Fact]
    public async Task IssueAsync_FallsBackToPathOnly_WhenFrontendBaseUrlMissing()
    {
        _users.Setup(u => u.GetByIdAsync(42, It.IsAny<CancellationToken>()))
              .ReturnsAsync(BuildUser());

        string? capturedUrl = null;
        _email.Setup(s => s.SendFirstLoginInvitationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, DateTime, CancellationToken>(
                (_, _, _, url, _, _) => capturedUrl = url)
            .Returns(Task.CompletedTask);

        await CreateSut(new FirstLoginOptions
        {
            TokenLifetimeHours = 72,
            FrontendBaseUrl = null,
            LinkPath = "/first-login?token={token}",
        }).IssueAsync(42);

        capturedUrl.Should().Be($"/first-login?token={Uri.EscapeDataString(ExtractTokenFromUrl(capturedUrl!))}");
        capturedUrl.Should().StartWith("/first-login?token=");
    }

    private static string ExtractTokenFromUrl(string url)
    {
        var idx = url.IndexOf("token=", StringComparison.Ordinal);
        return idx < 0 ? string.Empty : Uri.UnescapeDataString(url[(idx + "token=".Length)..]);
    }
}
