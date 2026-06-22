using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Infrastructure.Persistence;
using MPS.Infrastructure.Persistence.Interceptors;
using MPS.Infrastructure.Services;

namespace MPS.Infrastructure.Tests.Persistence.Interceptors;

/// <summary>
/// Integration-style test cho <see cref="AuditSaveChangesInterceptor"/> trên InMemory DB.
/// Dùng Position (Auditable, ít FK) làm proxy cho mọi entity nghiệp vụ.
/// AllowanceType (KHÔNG Auditable) verify rằng entity ngoài whitelist không sinh row.
/// </summary>
public sealed class AuditSaveChangesInterceptorTests
{
    private static (AppDbContext db, Mock<ICurrentUserService> currentUser) NewContext(
        int? userId = 1,
        string? username = "tester")
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.SetupGet(x => x.Username).Returns(username);

        var auditCtx = new Mock<IAuditContextProvider>();
        auditCtx.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        auditCtx.SetupGet(x => x.RequestPath).Returns("/api/test");
        auditCtx.SetupGet(x => x.CorrelationId).Returns("corr-123");

        var interceptor = new AuditSaveChangesInterceptor(
            currentUser.Object,
            auditCtx.Object,
            new DefaultAuditSeverityClassifier());

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"audit-int-{Guid.NewGuid():N}")
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(interceptor)
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return (db, currentUser);
    }

    [Fact]
    public async Task Create_Audited_Entity_Writes_One_Row_With_Action_Create()
    {
        var (db, _) = NewContext();
        await using var _db = db;

        db.Positions.Add(new Position { Code = "DEV", Name = "Developer" });
        await db.SaveChangesAsync();

        var logs = await db.AuditLogs.AsNoTracking().ToListAsync();
        logs.Should().HaveCount(1);

        var log = logs[0];
        log.Module.Should().Be("Position");
        log.Action.Should().Be(AuditAction.Create);
        log.UserId.Should().Be(1);
        log.UsernameSnapshot.Should().Be("tester");
        log.RequestPath.Should().Be("/api/test");
        log.IpAddress.Should().Be("127.0.0.1");
        log.DataDiff.Should().Contain("\"new\"");
    }

    [Fact]
    public async Task Update_Records_Only_Changed_Fields()
    {
        var (db, _) = NewContext();
        await using var _db = db;

        var pos = new Position { Code = "DEV", Name = "Developer" };
        db.Positions.Add(pos);
        await db.SaveChangesAsync();

        // Clear create-row noise
        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        pos.Name = "Senior Developer";
        await db.SaveChangesAsync();

        var logs = await db.AuditLogs.AsNoTracking().Where(x => x.Action == AuditAction.Update).ToListAsync();
        logs.Should().HaveCount(1);

        var log = logs[0];
        log.Module.Should().Be("Position");
        log.DataDiff.Should().Contain("Name");
        log.DataDiff.Should().NotContain("\"Code\"");
        log.DataDiff.Should().Contain("\"old\"");
        log.DataDiff.Should().Contain("\"new\"");
    }

    [Fact]
    public async Task Delete_Audited_Entity_Writes_Row_With_Action_Delete_And_Severity_High()
    {
        var (db, _) = NewContext();
        await using var _db = db;

        var pos = new Position { Code = "DEV", Name = "Developer" };
        db.Positions.Add(pos);
        await db.SaveChangesAsync();

        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        db.Positions.Remove(pos);
        await db.SaveChangesAsync();

        var logs = await db.AuditLogs.AsNoTracking().Where(x => x.Action == AuditAction.Delete).ToListAsync();
        logs.Should().HaveCount(1);
        logs[0].Severity.Should().Be(AuditSeverity.High);
        logs[0].DataDiff.Should().Contain("\"old\"");
    }

    [Fact]
    public async Task Non_Auditable_Entity_Does_Not_Generate_Row()
    {
        var (db, _) = NewContext();
        await using var _db = db;

        db.AllowanceTypes.Add(new AllowanceType
        {
            Code = "LUNCH",
            Name = "Lunch",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var logs = await db.AuditLogs.AsNoTracking().ToListAsync();
        logs.Should().BeEmpty();
    }

    [Fact]
    public async Task AuditLog_Itself_Is_Skipped_By_LoopGuard()
    {
        var (db, _) = NewContext();
        await using var _db = db;

        // Add an audit row directly (simulating AuditLogger path) — interceptor must NOT
        // recursively create another audit row for this AuditLog write.
        db.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            Module = "Manual",
            Action = AuditAction.Create,
            Severity = AuditSeverity.Low,
            RecordId = "x",
            DataDiff = "{}",
            UsernameSnapshot = "tester",
        });
        await db.SaveChangesAsync();

        var logs = await db.AuditLogs.AsNoTracking().ToListAsync();
        logs.Should().HaveCount(1, "exactly the one manual row; no loop");
    }

    [Fact]
    public async Task Multiple_Entities_In_Same_SaveChanges_Generate_Multiple_Rows()
    {
        var (db, _) = NewContext();
        await using var _db = db;

        db.Positions.AddRange(
            new Position { Code = "DEV", Name = "Developer" },
            new Position { Code = "QA", Name = "QA Engineer" });
        await db.SaveChangesAsync();

        var logs = await db.AuditLogs.AsNoTracking().ToListAsync();
        logs.Should().HaveCount(2);
        logs.Should().AllSatisfy(l => l.Module.Should().Be("Position"));
    }

    [Fact]
    public async Task Severity_Is_High_For_AlwaysHigh_Module_Create()
    {
        var (db, _) = NewContext();
        await using var _db = db;

        // Role is in AlwaysHighModules (tầng 2) — Create row should be High.
        db.Roles.Add(new Role { Name = "Auditor" });
        await db.SaveChangesAsync();

        var log = await db.AuditLogs.AsNoTracking().SingleAsync(x => x.Module == "Role");
        log.Severity.Should().Be(AuditSeverity.High);
    }

    [Fact]
    public async Task Severity_Is_Low_For_User_Create_And_Trivial_Update()
    {
        // User không còn nằm trong AlwaysHighModules — chỉ High khi sửa field bảo mật.
        var (db, _) = NewContext();
        await using var _db = db;

        var user = new User
        {
            EmployeeCode = "U1",
            FullName = "U",
            RoleId = 1,
            Username = "u1",
            PasswordHash = "x",
            Email = "u@x.local",
            Dob = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PhoneNo = "0",
            Status = 1,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var createLog = await db.AuditLogs.AsNoTracking().SingleAsync(x => x.Module == "User");
        createLog.Severity.Should().Be(AuditSeverity.Low);

        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var updateLog = await db.AuditLogs.AsNoTracking().SingleAsync(x => x.Module == "User");
        updateLog.Severity.Should().Be(AuditSeverity.Low);
    }

    [Fact]
    public async Task Severity_Is_High_When_User_PasswordHash_Or_RoleId_Or_Status_Changes()
    {
        var (db, _) = NewContext();
        await using var _db = db;

        var user = new User
        {
            EmployeeCode = "U2",
            FullName = "U",
            RoleId = 1,
            Username = "u2",
            PasswordHash = "old",
            Email = "u@x.local",
            Dob = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PhoneNo = "0",
            Status = 1,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        user.PasswordHash = "new-hash";
        user.RoleId = 2;
        user.Status = 0;
        await db.SaveChangesAsync();

        var updateLog = await db.AuditLogs.AsNoTracking().SingleAsync(x => x.Module == "User");
        updateLog.Severity.Should().Be(AuditSeverity.High);
    }

    [Fact]
    public async Task Username_Falls_Back_To_System_When_Anonymous()
    {
        var (db, _) = NewContext(userId: null, username: null);
        await using var _db = db;

        db.Positions.Add(new Position { Code = "DEV", Name = "Developer" });
        await db.SaveChangesAsync();

        var log = await db.AuditLogs.AsNoTracking().SingleAsync();
        log.UserId.Should().BeNull();
        log.UsernameSnapshot.Should().Be("system");
    }
}
