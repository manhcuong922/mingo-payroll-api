using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Infrastructure.Persistence;
using MPS.Infrastructure.Repositories;

namespace MPS.Infrastructure.Tests.Repositories;

/// <summary>
/// Tests cho <see cref="AuditLogRepository"/> trên InMemory DB.
/// Lưu ý: InMemory KHÔNG hỗ trợ ExecuteDeleteAsync nên các test delete-from-archive
/// được phủ qua <see cref="MPS.Infrastructure.Tests.BackgroundJobs.AuditLogRetentionJobTests"/>
/// (mock repo). Repo tests ở đây tập trung Add / QueryPaged / GetRecordHistory / Move.
/// </summary>
public sealed class AuditLogRepositoryTests
{
    private static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"audit-repo-{Guid.NewGuid():N}")
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static AuditLog NewLog(
        string module,
        AuditAction action,
        AuditSeverity severity,
        DateTime timestamp,
        int? userId = null,
        string recordId = "1") => new()
    {
        Timestamp = timestamp,
        UserId = userId,
        UsernameSnapshot = "tester",
        Module = module,
        Action = action,
        Severity = severity,
        RecordId = recordId,
        DataDiff = "{}",
    };

    [Fact]
    public async Task AddAsync_Persists_And_Returns_Id()
    {
        await using var db = NewContext();
        var repo = new AuditLogRepository(db);

        var id = await repo.AddAsync(NewLog("Contract", AuditAction.Create, AuditSeverity.Low, DateTime.UtcNow));

        id.Should().BeGreaterThan(0);
        (await db.AuditLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task QueryPagedAsync_Filters_By_Module_And_Severity()
    {
        await using var db = NewContext();
        var now = DateTime.UtcNow;
        db.AuditLogs.AddRange(
            NewLog("Contract", AuditAction.Update, AuditSeverity.High, now.AddMinutes(-10)),
            NewLog("Contract", AuditAction.Update, AuditSeverity.Low, now.AddMinutes(-9)),
            NewLog("User", AuditAction.Update, AuditSeverity.High, now.AddMinutes(-8)));
        await db.SaveChangesAsync();

        var repo = new AuditLogRepository(db);
        var (items, total) = await repo.QueryPagedAsync(
            userId: null, module: "Contract", action: null, severity: AuditSeverity.High,
            fromUtc: null, toUtc: null, page: 1, size: 20);

        total.Should().Be(1);
        items.Should().ContainSingle()
            .Which.Module.Should().Be("Contract");
    }

    [Fact]
    public async Task QueryPagedAsync_Orders_Desc_By_Timestamp()
    {
        await using var db = NewContext();
        var now = DateTime.UtcNow;
        db.AuditLogs.AddRange(
            NewLog("Contract", AuditAction.Update, AuditSeverity.Low, now.AddMinutes(-30), recordId: "A"),
            NewLog("Contract", AuditAction.Update, AuditSeverity.Low, now.AddMinutes(-10), recordId: "B"),
            NewLog("Contract", AuditAction.Update, AuditSeverity.Low, now.AddMinutes(-20), recordId: "C"));
        await db.SaveChangesAsync();

        var repo = new AuditLogRepository(db);
        var (items, _) = await repo.QueryPagedAsync(null, null, null, null, null, null, 1, 20);

        items.Select(x => x.RecordId).Should().ContainInOrder("B", "C", "A");
    }

    [Fact]
    public async Task QueryPagedAsync_Date_Range_Filter()
    {
        await using var db = NewContext();
        var anchor = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        db.AuditLogs.AddRange(
            NewLog("Contract", AuditAction.Create, AuditSeverity.Low, anchor.AddDays(-2), recordId: "OLD"),
            NewLog("Contract", AuditAction.Create, AuditSeverity.Low, anchor, recordId: "IN"),
            NewLog("Contract", AuditAction.Create, AuditSeverity.Low, anchor.AddDays(2), recordId: "AFTER"));
        await db.SaveChangesAsync();

        var repo = new AuditLogRepository(db);
        var (items, total) = await repo.QueryPagedAsync(
            userId: null, module: null, action: null, severity: null,
            fromUtc: anchor.AddDays(-1), toUtc: anchor.AddDays(1),
            page: 1, size: 20);

        total.Should().Be(1);
        items.Should().ContainSingle().Which.RecordId.Should().Be("IN");
    }

    [Fact]
    public async Task QueryPagedAsync_Respects_Page_And_Size()
    {
        await using var db = NewContext();
        var now = DateTime.UtcNow;
        for (var i = 0; i < 25; i++)
            db.AuditLogs.Add(NewLog("Contract", AuditAction.Update, AuditSeverity.Low,
                now.AddMinutes(-i), recordId: i.ToString()));
        await db.SaveChangesAsync();

        var repo = new AuditLogRepository(db);
        var (page1, total) = await repo.QueryPagedAsync(null, null, null, null, null, null, 1, 10);
        var (page3, _) = await repo.QueryPagedAsync(null, null, null, null, null, null, 3, 10);

        total.Should().Be(25);
        page1.Should().HaveCount(10);
        page3.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetRecordHistoryAsync_Returns_Chronological()
    {
        await using var db = NewContext();
        var t0 = DateTime.UtcNow.AddMinutes(-30);
        db.AuditLogs.AddRange(
            NewLog("Contract", AuditAction.Create, AuditSeverity.Low, t0.AddMinutes(20), recordId: "42"),
            NewLog("Contract", AuditAction.Update, AuditSeverity.High, t0.AddMinutes(10), recordId: "42"),
            NewLog("Contract", AuditAction.Create, AuditSeverity.Low, t0, recordId: "42"),
            NewLog("Contract", AuditAction.Create, AuditSeverity.Low, t0, recordId: "OTHER"));
        await db.SaveChangesAsync();

        var repo = new AuditLogRepository(db);
        var history = await repo.GetRecordHistoryAsync("Contract", "42");

        history.Should().HaveCount(3);
        history.Select(x => x.Action).Should().ContainInOrder(
            AuditAction.Create, AuditAction.Update, AuditAction.Create);
    }
}
