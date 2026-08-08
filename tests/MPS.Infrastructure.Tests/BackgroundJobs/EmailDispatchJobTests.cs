using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.BackgroundJobs;
using MPS.Infrastructure.Email;
using MPS.Infrastructure.Persistence;
using MPS.Infrastructure.Repositories;

namespace MPS.Infrastructure.Tests.BackgroundJobs;

/// <summary>
/// Tests cho <see cref="EmailDispatchJob.RunOnceAsync"/>: gửi OK → Sent (xóa payload);
/// lỗi → tăng Attempts + backoff; hết số lần thử → Failed; message chưa tới hạn → bỏ qua.
/// </summary>
public sealed class EmailDispatchJobTests
{
    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);
    }

    private sealed class Harness
    {
        public required ServiceProvider Provider { get; init; }
        public required FakeClock Clock { get; init; }
        public required Mock<IRealEmailSender> Sender { get; init; }
        public required EmailDispatchJob Job { get; init; }
        public required string DbName { get; init; }

        public AppDbContext NewContext() =>
            new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(DbName)
                .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
    }

    private static Harness BuildHarness(
        EmailDispatchOptions? options = null, IOutboxPayloadProtector? protector = null)
    {
        var dbName = $"dispatch-job-{Guid.NewGuid():N}";
        var clock = new FakeClock();
        var sender = new Mock<IRealEmailSender>(MockBehavior.Loose);

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddScoped<IEmailOutboxRepository, EmailOutboxRepository>();
        services.AddScoped<IClock>(_ => clock);
        services.AddScoped(_ => sender.Object);
        services.AddSingleton(protector ?? new NoOpOutboxPayloadProtector());

        var provider = services.BuildServiceProvider();

        var job = new EmailDispatchJob(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options ?? new EmailDispatchOptions()),
            NullLogger<EmailDispatchJob>.Instance);

        return new Harness { Provider = provider, Clock = clock, Sender = sender, Job = job, DbName = dbName };
    }

    private static async Task SeedPasswordResetAsync(Harness h)
    {
        using var scope = h.Provider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IEmailOutboxRepository>();
        var protector = scope.ServiceProvider.GetRequiredService<IOutboxPayloadProtector>();
        var sender = new OutboxEmailSender(repo, h.Clock, protector);
        await sender.SendPasswordResetAsync(
            "user@mingo.local", "TOKEN", "https://operix.fun/reset-password?token=TOKEN",
            h.Clock.UtcNow.AddHours(1));
    }

    [Fact]
    public async Task RunOnceAsync_SendsPendingMessage_MarksSent_AndClearsPayload()
    {
        // Arrange
        var h = BuildHarness();
        await SeedPasswordResetAsync(h);

        // Act
        var sent = await h.Job.RunOnceAsync(CancellationToken.None);

        // Assert
        sent.Should().Be(1);
        h.Sender.Verify(s => s.SendPasswordResetAsync(
            "user@mingo.local", "TOKEN", "https://operix.fun/reset-password?token=TOKEN",
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);

        await using var db = h.NewContext();
        var row = await db.EmailOutboxMessages.SingleAsync();
        row.Status.Should().Be(EmailOutboxStatus.Sent);
        row.SentAt.Should().Be(h.Clock.UtcNow);
        row.PayloadJson.Should().BeEmpty(); // dữ liệu nhạy cảm bị xóa sau khi gửi
    }

    [Fact]
    public async Task RunOnceAsync_OnSenderFailure_IncrementsAttempts_AndSchedulesBackoff()
    {
        // Arrange
        var h = BuildHarness(new EmailDispatchOptions { MaxAttempts = 5, BaseBackoffSeconds = 30 });
        h.Sender.Setup(s => s.SendPasswordResetAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));
        await SeedPasswordResetAsync(h);

        // Act
        var sent = await h.Job.RunOnceAsync(CancellationToken.None);

        // Assert
        sent.Should().Be(0);
        await using var db = h.NewContext();
        var row = await db.EmailOutboxMessages.SingleAsync();
        row.Status.Should().Be(EmailOutboxStatus.Pending);
        row.Attempts.Should().Be(1);
        row.LastError.Should().Contain("smtp down");
        row.NextAttemptAt.Should().Be(h.Clock.UtcNow.AddSeconds(30)); // 30 * 2^0
    }

    [Fact]
    public async Task RunOnceAsync_WhenAttemptsReachMax_MarksFailed()
    {
        // Arrange
        var h = BuildHarness(new EmailDispatchOptions { MaxAttempts = 3 });
        h.Sender.Setup(s => s.SendPasswordResetAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));
        await SeedPasswordResetAsync(h);

        // Đưa message tới ngưỡng: đã thử 2 lần, lần này là lần 3 = Max.
        await using (var seed = h.NewContext())
        {
            var msg = await seed.EmailOutboxMessages.SingleAsync();
            msg.Attempts = 2;
            await seed.SaveChangesAsync();
        }

        // Act
        await h.Job.RunOnceAsync(CancellationToken.None);

        // Assert
        await using var db = h.NewContext();
        var row = await db.EmailOutboxMessages.SingleAsync();
        row.Status.Should().Be(EmailOutboxStatus.Failed);
        row.Attempts.Should().Be(3);
        row.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task RunOnceAsync_WithAesProtector_StoresCiphertext_ButSendsDecryptedToken()
    {
        // Arrange — protector AES thật: payload lưu trong DB là ciphertext, worker giải mã khi gửi.
        var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var protector = new AesOutboxPayloadProtector(key);
        var h = BuildHarness(protector: protector);
        await SeedPasswordResetAsync(h);

        await using (var seed = h.NewContext())
        {
            var stored = await seed.EmailOutboxMessages.SingleAsync();
            stored.PayloadJson.Should().NotContain("TOKEN"); // đã mã hóa, không lộ token
        }

        string? capturedToken = null;
        h.Sender.Setup(s => s.SendPasswordResetAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, DateTime, CancellationToken>(
                (_, token, _, _, _) => capturedToken = token)
            .Returns(Task.CompletedTask);

        // Act
        var sent = await h.Job.RunOnceAsync(CancellationToken.None);

        // Assert — worker giải mã đúng token gốc.
        sent.Should().Be(1);
        capturedToken.Should().Be("TOKEN");
    }

    [Fact]
    public async Task RunOnceAsync_SkipsMessage_WhenNextAttemptInFuture()
    {
        // Arrange
        var h = BuildHarness();
        await SeedPasswordResetAsync(h);
        await using (var seed = h.NewContext())
        {
            var msg = await seed.EmailOutboxMessages.SingleAsync();
            msg.NextAttemptAt = h.Clock.UtcNow.AddMinutes(5); // chưa tới hạn
            await seed.SaveChangesAsync();
        }

        // Act
        var sent = await h.Job.RunOnceAsync(CancellationToken.None);

        // Assert
        sent.Should().Be(0);
        h.Sender.Verify(s => s.SendPasswordResetAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
