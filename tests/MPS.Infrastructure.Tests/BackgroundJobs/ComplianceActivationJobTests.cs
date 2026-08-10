using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities;
using MPS.Domain.Entities.Compliance;
using MPS.Infrastructure.BackgroundJobs;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Tests.BackgroundJobs;

/// <summary>
/// <see cref="ComplianceActivationJob"/> — bật cờ IsActive cho văn bản tới ngày hiệu lực.
///
/// <para>Trọng tâm: seed luôn giữ CẢ bản lịch sử (IsActive=false, ValidFrom đã qua) lẫn bản hiện hành,
/// để tính lại kỳ lương cũ vẫn ra đúng số của thời điểm đó. Job phải phân biệt được "bản chờ tới hạn"
/// với "bản lịch sử", nếu không hai bản sẽ giành cờ qua lại mỗi đêm và tiền lương đổi theo.</para>
/// </summary>
public sealed class ComplianceActivationJobTests
{
    private static readonly DateOnly Today = new(2026, 8, 10);

    private static AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"compliance-activation-{Guid.NewGuid():N}")
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static ComplianceActivationJob NewJob(AppDbContext db)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(Today.ToDateTime(TimeOnly.MinValue));

        var sp = new ServiceCollection()
            .AddSingleton(db)
            .AddSingleton(clock.Object)
            .AddSingleton(Mock.Of<IPublisher>())
            .BuildServiceProvider();

        return new ComplianceActivationJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ComplianceActivationJob>.Instance);
    }

    private static LaborLawConstantsVersion Version(int id, DateOnly validFrom, bool isActive) => new()
    {
        Id = id,
        ValidFrom = validFrom,
        IsActive = isActive,
        IsArchived = false,
        LegalReference = $"VB-{id}",
        CreatedAt = DateTime.UtcNow,
        CreatedByUserId = 1,
    };

    private static async Task<AppDbContext> SeedAsync(params LaborLawConstantsVersion[] versions)
    {
        var db = NewContext();
        db.Users.Add(new User { Id = 1, Username = "admin", PasswordHash = "x", EmployeeCode = "AD001" });
        db.LaborLawConstantsVersions.AddRange(versions);
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task BanLichSu_KhongDuocGianhCoTuBanHienHanh()
    {
        // Đúng hình dạng seed thật: bản lịch sử 2021 (đã tắt cờ) + bản hiện hành 01/07/2026.
        var db = await SeedAsync(
            Version(1, new DateOnly(2021, 1, 1), isActive: false),
            Version(2, new DateOnly(2026, 7, 1), isActive: true));

        var activated = await NewJob(db).RunOnceAsync(CancellationToken.None);

        activated.Should().Be(0, "không có văn bản nào muộn hơn bản đang áp dụng");
        (await db.LaborLawConstantsVersions.FindAsync(2))!.IsActive
            .Should().BeTrue("bản 01/07/2026 phải giữ hiệu lực");
        (await db.LaborLawConstantsVersions.FindAsync(1))!.IsActive
            .Should().BeFalse("bản lịch sử 2021 không được bật lại");
    }

    [Fact]
    public async Task ChayNhieuDem_TrangThaiOnDinh_KhongLatQuaLatLai()
    {
        var db = await SeedAsync(
            Version(1, new DateOnly(2021, 1, 1), isActive: false),
            Version(2, new DateOnly(2026, 7, 1), isActive: true));
        var job = NewJob(db);

        for (var night = 0; night < 3; night++)
            await job.RunOnceAsync(CancellationToken.None);

        (await db.LaborLawConstantsVersions.FindAsync(2))!.IsActive.Should().BeTrue();
        (await db.LaborLawConstantsVersions.FindAsync(1))!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task BanChoToiNgayHieuLuc_VanDuocKichHoat_VaHaBanCu()
    {
        // Bản mới 01/08/2026 đã tới hạn (hôm nay 10/08) ⇒ phải thay bản 01/07/2026.
        var db = await SeedAsync(
            Version(1, new DateOnly(2026, 7, 1), isActive: true),
            Version(2, new DateOnly(2026, 8, 1), isActive: false));

        var activated = await NewJob(db).RunOnceAsync(CancellationToken.None);

        activated.Should().Be(1);
        (await db.LaborLawConstantsVersions.FindAsync(2))!.IsActive.Should().BeTrue();
        (await db.LaborLawConstantsVersions.FindAsync(1))!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task BanChuaToiNgayHieuLuc_KhongDuocKichHoatSom()
    {
        var db = await SeedAsync(
            Version(1, new DateOnly(2026, 7, 1), isActive: true),
            Version(2, new DateOnly(2026, 12, 1), isActive: false));

        var activated = await NewJob(db).RunOnceAsync(CancellationToken.None);

        activated.Should().Be(0);
        (await db.LaborLawConstantsVersions.FindAsync(2))!.IsActive.Should().BeFalse();
        (await db.LaborLawConstantsVersions.FindAsync(1))!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ChuaCoBanNaoActive_ChonBanCoHieuLucMuonNhatDaToiHan()
    {
        var db = await SeedAsync(
            Version(1, new DateOnly(2025, 7, 1), isActive: false),
            Version(2, new DateOnly(2026, 7, 1), isActive: false));

        var activated = await NewJob(db).RunOnceAsync(CancellationToken.None);

        activated.Should().Be(1);
        (await db.LaborLawConstantsVersions.FindAsync(2))!.IsActive.Should().BeTrue();
        (await db.LaborLawConstantsVersions.FindAsync(1))!.IsActive.Should().BeFalse();
    }
}
