using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums;
using MPS.Infrastructure.Persistence;
using MPS.Infrastructure.Services;

namespace MPS.Infrastructure.Tests.Services;

public sealed class AuditLoggerTests
{
    private static (AppDbContext db, AuditLogger logger) NewSut(
        int? userId = 7,
        string? username = "alice",
        string? ip = "10.0.0.1")
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.SetupGet(x => x.Username).Returns(username);

        var auditCtx = new Mock<IAuditContextProvider>();
        auditCtx.SetupGet(x => x.IpAddress).Returns(ip);
        auditCtx.SetupGet(x => x.RequestPath).Returns("/api/x");
        auditCtx.SetupGet(x => x.CorrelationId).Returns("corr");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"audit-logger-{Guid.NewGuid():N}")
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        return (db, new AuditLogger(db, currentUser.Object, auditCtx.Object, new DefaultAuditSeverityClassifier()));
    }

    [Fact]
    public async Task LogAccessDeniedAsync_Writes_Row_With_AccessDenied_Low()
    {
        var (db, logger) = NewSut();
        await using var _db = db;

        await logger.LogAccessDeniedAsync("/api/audit-logs", "GET /api/audit-logs", CancellationToken.None);

        var log = await db.AuditLogs.AsNoTracking().SingleAsync();
        log.Action.Should().Be(AuditAction.AccessDenied);
        log.Severity.Should().Be(AuditSeverity.Low, "AccessDenied is security telemetry, not a legal data-change");
        log.RequestPath.Should().Be("/api/audit-logs");
        log.UserId.Should().Be(7);
        log.UsernameSnapshot.Should().Be("alice");
        log.IpAddress.Should().Be("10.0.0.1");
        log.Module.Should().Be("AccessControl");
    }

    [Fact]
    public async Task LogAccessDeniedAsync_With_Anonymous_User_Uses_Anonymous_Username()
    {
        var (db, logger) = NewSut(userId: null, username: null);
        await using var _db = db;

        await logger.LogAccessDeniedAsync("/api/x", null, CancellationToken.None);

        var log = await db.AuditLogs.AsNoTracking().SingleAsync();
        log.UserId.Should().BeNull();
        log.UsernameSnapshot.Should().Be("anonymous");
    }

    [Fact]
    public async Task LogManualAsync_Persists_Entry_As_Given()
    {
        var (db, logger) = NewSut();
        await using var _db = db;

        var entry = new AuditLogEntry(
            Module: "PayrollLine",
            Action: AuditAction.Update,
            Severity: AuditSeverity.High,
            RecordId: "42",
            DataDiff: "{\"Amount\":{\"old\":100,\"new\":200}}");

        await logger.LogManualAsync(entry, CancellationToken.None);

        var log = await db.AuditLogs.AsNoTracking().SingleAsync();
        log.Module.Should().Be("PayrollLine");
        log.Action.Should().Be(AuditAction.Update);
        log.Severity.Should().Be(AuditSeverity.High);
        log.RecordId.Should().Be("42");
        log.DataDiff.Should().Contain("Amount");
    }
}
