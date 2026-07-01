using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.BackgroundJobs;
using MPS.Infrastructure.Email;
using MPS.Infrastructure.Persistence;
using MPS.Infrastructure.Repositories;

namespace MPS.Infrastructure.Tests.BackgroundJobs;

/// <summary>
/// <see cref="EmailOutboxCleanupJob"/> chỉ xóa dòng Sent cũ hơn ngưỡng; giữ lại Sent gần đây và mọi Pending.
/// </summary>
public sealed class EmailOutboxCleanupJobTests
{
    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    [Fact]
    public async Task RunOnceAsync_DeletesOldSent_KeepsRecentSentAndPending()
    {
        // Arrange
        var dbName = $"outbox-cleanup-{Guid.NewGuid():N}";
        var clock = new FakeClock();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddScoped<IEmailOutboxRepository, EmailOutboxRepository>();
        services.AddScoped<IClock>(_ => clock);
        var provider = services.BuildServiceProvider();

        AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

        await using (var seed = NewContext())
        {
            seed.EmailOutboxMessages.AddRange(
                Msg(EmailOutboxStatus.Sent, sentAt: clock.UtcNow.AddDays(-10)), // cũ → xóa (retention 7)
                Msg(EmailOutboxStatus.Sent, sentAt: clock.UtcNow.AddDays(-2)),  // gần đây → giữ
                Msg(EmailOutboxStatus.Pending, sentAt: null));                  // pending → giữ
            await seed.SaveChangesAsync();
        }

        var job = new EmailOutboxCleanupJob(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new EmailDispatchOptions { SentRetentionDays = 7 }),
            NullLogger<EmailOutboxCleanupJob>.Instance);

        // Act
        var deleted = await job.RunOnceAsync(CancellationToken.None);

        // Assert
        deleted.Should().Be(1);
        await using var db = NewContext();
        var remaining = await db.EmailOutboxMessages.ToListAsync();
        remaining.Should().HaveCount(2);
        remaining.Should().NotContain(m => m.Status == EmailOutboxStatus.Sent && m.SentAt < clock.UtcNow.AddDays(-7));
    }

    private static EmailOutboxMessage Msg(EmailOutboxStatus status, DateTime? sentAt) => new()
    {
        Type = EmailOutboxType.PasswordReset,
        ToEmail = "user@mingo.local",
        PayloadJson = "{}",
        Status = status,
        CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        SentAt = sentAt,
    };
}
