using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums;
using MPS.Infrastructure.Email;
using MPS.Infrastructure.Persistence;
using MPS.Infrastructure.Repositories;

namespace MPS.Infrastructure.Tests.Email;

/// <summary>
/// <see cref="OutboxEmailSender"/> KHÔNG gửi email — chỉ ghi một dòng Pending vào bảng outbox.
/// Test xác nhận từng loại email ghi đúng cột + payload chứa dữ liệu cần thiết.
/// </summary>
public sealed class OutboxEmailSenderTests
{
    private static AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-sender-{Guid.NewGuid():N}")
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);
    }

    [Fact]
    public async Task SendPasswordResetAsync_WritesPendingRow_WithTokenInPayload()
    {
        // Arrange
        await using var db = NewContext();
        var clock = new FakeClock();
        var sender = new OutboxEmailSender(new EmailOutboxRepository(db), clock, new NoOpOutboxPayloadProtector());

        // Act
        await sender.SendPasswordResetAsync("user@mingo.local", "RESET-TOKEN-123", clock.UtcNow.AddHours(1));

        // Assert
        var row = await db.EmailOutboxMessages.SingleAsync();
        row.Type.Should().Be(EmailOutboxType.PasswordReset);
        row.ToEmail.Should().Be("user@mingo.local");
        row.Status.Should().Be(EmailOutboxStatus.Pending);
        row.Attempts.Should().Be(0);
        row.CreatedAt.Should().Be(clock.UtcNow);
        row.PayloadJson.Should().Contain("RESET-TOKEN-123");
        row.StorageBucket.Should().BeNull();
        row.StorageObjectKey.Should().BeNull();
    }

    /// <summary>Backward-compat test: dữ liệu outbox cũ (trước plan §2 N8) vẫn phải ghi đúng.
    /// Caller mới KHÔNG được dùng API deprecated này — dùng SendFirstLoginInvitationAsync.</summary>
#pragma warning disable CS0618
    [Fact]
    public async Task SendAccountCredentialsAsync_WritesPendingRow_WithCredentialsInPayload()
    {
        await using var db = NewContext();
        var sender = new OutboxEmailSender(new EmailOutboxRepository(db), new FakeClock(), new NoOpOutboxPayloadProtector());

        await sender.SendAccountCredentialsAsync("newbie@mingo.local", "Nguyen Van A", "nva", "Secr3t!");

        var row = await db.EmailOutboxMessages.SingleAsync();
        row.Type.Should().Be(EmailOutboxType.AccountCredentials);
        row.PayloadJson.Should().Contain("nva").And.Contain("Secr3t!");
    }
#pragma warning restore CS0618

    [Fact]
    public async Task SendFirstLoginInvitationAsync_WritesPendingRow_WithInvitationUrlInPayload()
    {
        await using var db = NewContext();
        var clock = new FakeClock();
        var sender = new OutboxEmailSender(new EmailOutboxRepository(db), clock, new NoOpOutboxPayloadProtector());

        var expiresAt = clock.UtcNow.AddHours(72);
        await sender.SendFirstLoginInvitationAsync(
            "newbie@mingo.local", "Nguyen Van A", "nva",
            "https://mps.local/first-login?token=abc",
            expiresAt);

        var row = await db.EmailOutboxMessages.SingleAsync();
        row.Type.Should().Be(EmailOutboxType.FirstLoginInvitation);
        row.ToEmail.Should().Be("newbie@mingo.local");
        row.PayloadJson.Should().Contain("nva")
            .And.Contain("first-login")
            .And.Contain("abc"); // token nằm trong invitation URL
    }

    [Fact]
    public async Task SendContractActivationAsync_WritesPendingRow_WithStorageKeyAndPayload()
    {
        await using var db = NewContext();
        var sender = new OutboxEmailSender(new EmailOutboxRepository(db), new FakeClock(), new NoOpOutboxPayloadProtector());
        var payload = new ContractActivationEmailPayload(
            ContractNo: "HD-2026-001",
            EffectiveFrom: new DateTime(2026, 7, 1),
            EffectiveTo: null);

        await sender.SendContractActivationAsync(
            "user@mingo.local", "Nguyen Van A", payload,
            storageBucket: "mps-storage", storageObjectKey: "contracts/HD-2026-001/v1.pdf",
            pdfFileName: "HD-2026-001.pdf");

        var row = await db.EmailOutboxMessages.SingleAsync();
        row.Type.Should().Be(EmailOutboxType.ContractActivation);
        row.StorageBucket.Should().Be("mps-storage");
        row.StorageObjectKey.Should().Be("contracts/HD-2026-001/v1.pdf");
        row.PayloadJson.Should().Contain("HD-2026-001").And.Contain("HD-2026-001.pdf");
    }
}
