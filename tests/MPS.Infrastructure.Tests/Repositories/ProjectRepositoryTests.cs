using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Infrastructure.Persistence;
using MPS.Infrastructure.Repositories;

namespace MPS.Infrastructure.Tests.Repositories;

/// <summary>
/// Tests cho <see cref="ProjectRepository"/> trên InMemory DB — tập trung filter theo status
/// (tính năng mới: phân loại đơn Active/Completed/Paused/Cancelled trên trang danh sách).
/// </summary>
public sealed class ProjectRepositoryTests
{
    private const int CreatorUserId = 1;

    private static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"project-repo-{Guid.NewGuid():N}")
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static Project NewProject(string title, ProjectStatus status, DateTime? deletedAt = null) => new()
    {
        Title = title,
        Status = status,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        CreatedByUserId = CreatorUserId,
        DeletedAt = deletedAt,
    };

    // Project.CreatedByUserId là FK bắt buộc — phải seed User thật, nếu không EF Core InMemory sẽ trả
    // rỗng cho mọi query có .Include(p => p.CreatedBy) (provider quirk khi Include trỏ tới FK "mồ côi").
    private static async Task<AppDbContext> SeedMixedStatusesAsync()
    {
        var db = NewContext();
        db.Users.Add(new User { Id = CreatorUserId, Username = "creator", PasswordHash = "x" });
        db.Projects.AddRange(
            NewProject("Active 1", ProjectStatus.Active),
            NewProject("Active 2", ProjectStatus.Active),
            NewProject("Completed 1", ProjectStatus.Completed),
            NewProject("Paused 1", ProjectStatus.Paused),
            NewProject("Cancelled 1", ProjectStatus.Cancelled, deletedAt: DateTime.UtcNow));
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task GetAllAsync_FiltersByStatus_WhenStatusProvided()
    {
        using var db = await SeedMixedStatusesAsync();
        var repo = new ProjectRepository(db);

        var result = await repo.GetAllAsync(CancellationToken.None, includeCancelled: true, status: ProjectStatus.Active);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(p => p.Status == ProjectStatus.Active);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllStatuses_WhenStatusIsNull()
    {
        using var db = await SeedMixedStatusesAsync();
        var repo = new ProjectRepository(db);

        var result = await repo.GetAllAsync(CancellationToken.None, includeCancelled: true, status: null);

        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetAllAsync_CombinesStatusFilter_WithIncludeCancelledFalse()
    {
        using var db = await SeedMixedStatusesAsync();
        var repo = new ProjectRepository(db);

        // Cancelled project has DeletedAt set — includeCancelled=false hides it even if status matched.
        var result = await repo.GetAllAsync(CancellationToken.None, includeCancelled: false, status: ProjectStatus.Cancelled);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByStatus_WhenStatusProvided()
    {
        using var db = await SeedMixedStatusesAsync();
        var repo = new ProjectRepository(db);

        var (items, total) = await repo.GetPagedAsync(
            keyword: null, departmentId: null, page: 1, pageSize: 20, CancellationToken.None,
            includeCancelled: true, status: ProjectStatus.Paused);

        total.Should().Be(1);
        items.Should().ContainSingle(p => p.Title == "Paused 1");
    }

    [Fact]
    public async Task GetByDepartmentAsync_FiltersByStatus_WhenStatusProvided()
    {
        using var db = NewContext();
        db.Users.Add(new User { Id = CreatorUserId, Username = "creator", PasswordHash = "x" });
        var active = NewProject("Active dept", ProjectStatus.Active);
        active.DepartmentId = 3;
        var completed = NewProject("Completed dept", ProjectStatus.Completed);
        completed.DepartmentId = 3;
        db.Projects.AddRange(active, completed);
        await db.SaveChangesAsync();
        var repo = new ProjectRepository(db);

        var result = await repo.GetByDepartmentAsync(3, CancellationToken.None, status: ProjectStatus.Completed);

        result.Should().ContainSingle(p => p.Title == "Completed dept");
    }
}
