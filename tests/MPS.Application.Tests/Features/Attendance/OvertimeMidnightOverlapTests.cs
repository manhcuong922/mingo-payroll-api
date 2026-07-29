using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Infrastructure.Persistence;
using MPS.Infrastructure.Repositories.Attendance;

namespace MPS.Application.Tests.Features.Attendance;

/// <summary>
/// #41 — OT vắt nửa đêm lọt guard trùng giờ. Đơn 22:00→02:00 lưu WorkDate=D nhưng chồng giờ thật
/// với đơn 01:00→03:00 của D+1. Guard cũ chỉ nạp OT có WorkDate==D ⇒ bỏ sót; phải quét D±1.
/// </summary>
public sealed class OvertimeMidnightOverlapTests
{
    private static readonly DateOnly D = new(2026, 6, 10);

    // ── Phần 1: OvertimeTimeOverlap tính đúng cho ca vắt nửa đêm (đã đúng, ghim chống hồi quy) ──

    [Fact]
    public void Overlap_Detects_CrossMidnight_Conflict_Between_D_And_NextDay()
    {
        // D 22:00→02:00 (chạm tới D+1 02:00) vs D+1 01:00→03:00 ⇒ chồng 01:00–02:00.
        OvertimeTimeOverlap.Overlaps(
            D, new TimeOnly(22, 0), new TimeOnly(2, 0),
            D.AddDays(1), new TimeOnly(1, 0), new TimeOnly(3, 0))
            .Should().BeTrue();
    }

    [Fact]
    public void Overlap_No_Conflict_When_NextDay_Starts_After_Midnight_Segment_Ends()
    {
        // D 22:00→02:00 vs D+1 03:00→05:00 ⇒ không chồng.
        OvertimeTimeOverlap.Overlaps(
            D, new TimeOnly(22, 0), new TimeOnly(2, 0),
            D.AddDays(1), new TimeOnly(3, 0), new TimeOnly(5, 0))
            .Should().BeFalse();
    }

    // ── Phần 2: repository quét đúng cửa sổ D±1 ──

    [Fact]
    public async Task GetActiveInRange_Fetches_Neighbor_Day_That_OnDate_Misses()
    {
        await using var db = NewDb();
        await SeedOtAsync(db, id: 1, workDate: D, start: new TimeOnly(22, 0), end: new TimeOnly(2, 0));
        await SeedOtAsync(db, id: 2, workDate: D.AddDays(1), start: new TimeOnly(1, 0), end: new TimeOnly(3, 0));
        await db.SaveChangesAsync();

        var repo = new OvertimeRequestRepository(db);

        // Guard cũ (chỉ WorkDate==D+1) không thấy đơn của D.
        var onDate = await repo.GetActiveOnDateForEmployeeAsync(employeeInformationId: 42, D.AddDays(1), excludeId: 2, CancellationToken.None);
        onDate.Should().BeEmpty();

        // Guard mới quét D±1 quanh D+1 ⇒ thấy đơn của D.
        var inRange = await repo.GetActiveInRangeForEmployeeAsync(
            employeeInformationId: 42, D, D.AddDays(2), excludeId: 2, CancellationToken.None);
        inRange.Should().ContainSingle().Which.Id.Should().Be(1);

        // Kết hợp overlap ⇒ phát hiện đúng conflict vắt nửa đêm.
        var neighbor = inRange.Single();
        OvertimeTimeOverlap.Overlaps(
            D.AddDays(1), new TimeOnly(1, 0), new TimeOnly(3, 0),
            neighbor.WorkDate, neighbor.OtStartTime, neighbor.OtEndTime)
            .Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveInRange_Excludes_Rejected_And_Cancelled()
    {
        await using var db = NewDb();
        await SeedOtAsync(db, id: 1, workDate: D, start: new TimeOnly(22, 0), end: new TimeOnly(2, 0), status: OtRequestStatus.Rejected);
        await SeedOtAsync(db, id: 2, workDate: D, start: new TimeOnly(18, 0), end: new TimeOnly(20, 0), status: OtRequestStatus.Cancelled);
        await db.SaveChangesAsync();

        var repo = new OvertimeRequestRepository(db);
        var inRange = await repo.GetActiveInRangeForEmployeeAsync(
            employeeInformationId: 42, D.AddDays(-1), D.AddDays(1), excludeId: null, CancellationToken.None);

        inRange.Should().BeEmpty("Rejected/Cancelled không còn chiếm khung giờ");
    }

    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ot-overlap-{Guid.NewGuid():N}")
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task SeedOtAsync(
        AppDbContext db, int id, DateOnly workDate, TimeOnly start, TimeOnly end,
        OtRequestStatus status = OtRequestStatus.Approved)
    {
        await db.OvertimeRequests.AddAsync(new OvertimeRequest
        {
            Id = id,
            EmployeeInformationId = 42,
            WorkDate = workDate,
            OtStartTime = start,
            OtEndTime = end,
            OtHours = 4m,
            Status = status,
            CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedByUserId = 1,
        });
    }
}
