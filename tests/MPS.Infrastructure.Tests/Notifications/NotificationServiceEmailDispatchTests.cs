using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Notifications.Abstractions;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Application.Features.Notifications.Templates;
using MPS.Domain.Entities.Notifications;
using MPS.Domain.Enums.Notifications;
using MPS.Domain.Interfaces.Notifications;
using MPS.Infrastructure.Notifications;
using Xunit;

namespace MPS.Infrastructure.Tests.Notifications;

/// <summary>
/// Phase 1 kênh email — verify NotificationService.CreateManyAsync:
///   • Default: lưu in-app + dispatch email.
///   • InAppOnly: lưu in-app + KHÔNG dispatch email dù có EmailContent.
///   • EmailOnly: KHÔNG lưu in-app + dispatch email vào email cá nhân (N3).
///   • Email fail không hỏng in-app.
///   • Không có email content ⇒ không call resolver / sender.
/// </summary>
public sealed class NotificationServiceEmailDispatchTests
{
    private readonly Mock<INotificationRepository> _repo = new();
    private readonly Mock<IRealtimePush> _push = new();
    private readonly Mock<IEmailSender> _email = new();
    private readonly Mock<IUserEmailResolver> _emailResolver = new();

    private NotificationService CreateSut() =>
        new(_repo.Object, _push.Object, _email.Object, _emailResolver.Object, NullLogger<NotificationService>.Instance);

    private static NotificationDraft Draft(
        int userId,
        EmailContent? email = null,
        NotificationDeliveryMode mode = NotificationDeliveryMode.Default,
        bool prefersPersonal = false) =>
        new(userId, NotificationType.System, NotificationCategory.System, NotificationSeverity.Info,
            "Title", "Body", null, null, email, mode, prefersPersonal);

    [Fact]
    public async Task Default_SavesInApp_AndDispatchesEmail()
    {
        _emailResolver.Setup(r => r.ResolveDeliveryEmailsAsync(
                It.IsAny<IReadOnlyCollection<int>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string> { [42] = "user@company.vn" });

        var email = new EmailContent("Subject", "Hello");
        await CreateSut().CreateManyAsync(new[] { Draft(42, email) }, CancellationToken.None);

        _repo.Verify(r => r.AddRangeAsync(
                It.Is<IEnumerable<Notification>>(list => list.Count() == 1),
                It.IsAny<CancellationToken>()),
            Times.Once, "phải lưu in-app");
        _email.Verify(s => s.SendGenericNotificationAsync(
                "user@company.vn", "Subject", "Hello", null, It.IsAny<CancellationToken>()),
            Times.Once, "phải dispatch email");
    }

    [Fact]
    public async Task InAppOnly_SavesInApp_ButDoesNotDispatchEmail()
    {
        var email = new EmailContent("S", "B");
        await CreateSut().CreateManyAsync(
            new[] { Draft(42, email, NotificationDeliveryMode.InAppOnly) },
            CancellationToken.None);

        _repo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()), Times.Once);
        _email.Verify(s => s.SendGenericNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _emailResolver.Verify(r => r.ResolveDeliveryEmailsAsync(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EmailOnly_SkipsInApp_AndDispatchesToPersonalEmail()
    {
        _emailResolver.Setup(r => r.ResolveDeliveryEmailsAsync(
                It.IsAny<IReadOnlyCollection<int>>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string> { [42] = "personal@gmail.com" });

        var email = new EmailContent("Locked", "Your account was locked");
        await CreateSut().CreateManyAsync(
            new[] { Draft(42, email, NotificationDeliveryMode.EmailOnly, prefersPersonal: true) },
            CancellationToken.None);

        _repo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()), Times.Never,
            "EmailOnly ⇒ bỏ in-app");
        _email.Verify(s => s.SendGenericNotificationAsync(
                "personal@gmail.com", "Locked", "Your account was locked", null, It.IsAny<CancellationToken>()),
            Times.Once);
        _emailResolver.Verify(r => r.ResolveDeliveryEmailsAsync(
                It.IsAny<IReadOnlyCollection<int>>(), true, It.IsAny<CancellationToken>()),
            Times.Once, "phải tra email cá nhân");
    }

    [Fact]
    public async Task DraftWithoutEmail_DoesNotCallEmailResolverOrSender()
    {
        await CreateSut().CreateManyAsync(new[] { Draft(42) }, CancellationToken.None);

        _repo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()), Times.Once);
        _emailResolver.VerifyNoOtherCalls();
        _email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EmailFailure_DoesNotThrow_InAppAlreadySaved()
    {
        _emailResolver.Setup(r => r.ResolveDeliveryEmailsAsync(
                It.IsAny<IReadOnlyCollection<int>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string> { [42] = "u@x.vn" });
        _email.Setup(s => s.SendGenericNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("outbox down"));

        var email = new EmailContent("S", "B");
        Func<Task> act = () => CreateSut().CreateManyAsync(new[] { Draft(42, email) }, CancellationToken.None);

        await act.Should().NotThrowAsync("email fail không được ảnh hưởng in-app");
        _repo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NoResolvedEmail_LogsAndSkips_DoesNotCallSender()
    {
        _emailResolver.Setup(r => r.ResolveDeliveryEmailsAsync(
                It.IsAny<IReadOnlyCollection<int>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string>());

        var email = new EmailContent("S", "B");
        await CreateSut().CreateManyAsync(new[] { Draft(42, email) }, CancellationToken.None);

        _email.Verify(s => s.SendGenericNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
