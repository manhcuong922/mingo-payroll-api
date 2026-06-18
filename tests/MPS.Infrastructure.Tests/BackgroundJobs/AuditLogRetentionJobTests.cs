using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MPS.Application.Options;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.BackgroundJobs;

namespace MPS.Infrastructure.Tests.BackgroundJobs;

/// <summary>
/// Verify orchestration của <see cref="AuditLogRetentionJob"/> qua mocked repo:
///  - bước 1 dùng ActiveRetentionDays global
///  - bước 2a gọi per-module overrides
///  - bước 2b sweep default cho module không khai báo override
///  - bước 3 skip khi HighValueArchiveRetentionDays null
///  - một sub-step fail không kéo các sub-step khác
/// </summary>
public sealed class AuditLogRetentionJobTests
{
    private static AuditLogRetentionJob NewJob(IAuditLogRepository repo, AuditLogOptions options)
    {
        var sp = new ServiceCollection()
            .AddSingleton(repo)
            .AddSingleton<IOptions<AuditLogOptions>>(Microsoft.Extensions.Options.Options.Create(options))
            .BuildServiceProvider();

        return new AuditLogRetentionJob(sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AuditLogRetentionJob>.Instance);
    }

    [Fact]
    public async Task RunOnceAsync_Calls_Move_With_Active_Retention_Cutoff()
    {
        var repo = new Mock<IAuditLogRepository>();
        var options = new AuditLogOptions
        {
            ActiveRetentionDays = 30,
            LowValueArchiveRetentionDays = 365,
            HighValueArchiveRetentionDays = null,
        };

        var job = NewJob(repo.Object, options);
        await job.RunOnceAsync(CancellationToken.None);

        repo.Verify(r => r.MoveOlderThanAsync(
            It.Is<DateTime>(d => d <= DateTime.UtcNow.AddDays(-30).AddMinutes(1)
                                  && d >= DateTime.UtcNow.AddDays(-30).AddMinutes(-1)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_Step2a_Calls_PerModule_Override_With_Module_Cutoff()
    {
        var repo = new Mock<IAuditLogRepository>();
        var options = new AuditLogOptions
        {
            ActiveRetentionDays = 30,
            LowValueArchiveRetentionDays = 365,
            LowValueArchiveRetentionByModule = new()
            {
                ["EmployeeInformation"] = 2555,
                ["Contract"] = 1825,
            },
            HighValueArchiveRetentionDays = null,
        };

        var job = NewJob(repo.Object, options);
        await job.RunOnceAsync(CancellationToken.None);

        repo.Verify(r => r.DeleteFromArchiveForModuleAsync(
            AuditSeverity.Low, "EmployeeInformation",
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.DeleteFromArchiveForModuleAsync(
            AuditSeverity.Low, "Contract",
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_Step2b_Excludes_Overridden_Modules()
    {
        var repo = new Mock<IAuditLogRepository>();
        var options = new AuditLogOptions
        {
            ActiveRetentionDays = 30,
            LowValueArchiveRetentionDays = 1825,
            LowValueArchiveRetentionByModule = new()
            {
                ["EmployeeInformation"] = 2555,
                ["Contract"] = 1825,
            },
            HighValueArchiveRetentionDays = null,
        };

        var job = NewJob(repo.Object, options);
        await job.RunOnceAsync(CancellationToken.None);

        repo.Verify(r => r.DeleteFromArchiveExceptModulesAsync(
            AuditSeverity.Low,
            It.Is<IReadOnlyCollection<string>>(c =>
                c.Contains("EmployeeInformation") && c.Contains("Contract")),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_Step3_Skipped_When_HighRetention_Null()
    {
        var repo = new Mock<IAuditLogRepository>();
        var options = new AuditLogOptions
        {
            ActiveRetentionDays = 30,
            LowValueArchiveRetentionDays = 365,
            HighValueArchiveRetentionDays = null,
        };

        var job = NewJob(repo.Object, options);
        await job.RunOnceAsync(CancellationToken.None);

        repo.Verify(r => r.DeleteFromArchiveAsync(
                AuditSeverity.High, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunOnceAsync_Step3_Runs_When_HighRetention_Set()
    {
        var repo = new Mock<IAuditLogRepository>();
        var options = new AuditLogOptions
        {
            ActiveRetentionDays = 30,
            LowValueArchiveRetentionDays = 365,
            HighValueArchiveRetentionDays = 730,
        };

        var job = NewJob(repo.Object, options);
        await job.RunOnceAsync(CancellationToken.None);

        repo.Verify(r => r.DeleteFromArchiveAsync(
                AuditSeverity.High, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_Step2a_Failure_Does_Not_Block_Step2b_Or_Step3()
    {
        var repo = new Mock<IAuditLogRepository>();
        repo.Setup(r => r.DeleteFromArchiveForModuleAsync(
                AuditSeverity.Low, "Contract", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var options = new AuditLogOptions
        {
            ActiveRetentionDays = 30,
            LowValueArchiveRetentionDays = 365,
            LowValueArchiveRetentionByModule = new()
            {
                ["EmployeeInformation"] = 2555,
                ["Contract"] = 1825,
            },
            HighValueArchiveRetentionDays = 730,
        };

        var job = NewJob(repo.Object, options);
        await job.RunOnceAsync(CancellationToken.None);

        // Step2a EmployeeInformation vẫn được thử dù Contract throw
        repo.Verify(r => r.DeleteFromArchiveForModuleAsync(
            AuditSeverity.Low, "EmployeeInformation",
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        // Step2b vẫn chạy
        repo.Verify(r => r.DeleteFromArchiveExceptModulesAsync(
            AuditSeverity.Low, It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        // Step3 vẫn chạy
        repo.Verify(r => r.DeleteFromArchiveAsync(
            AuditSeverity.High, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_Step1_Failure_Does_Not_Block_Later_Steps()
    {
        var repo = new Mock<IAuditLogRepository>();
        repo.Setup(r => r.MoveOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var options = new AuditLogOptions
        {
            ActiveRetentionDays = 30,
            LowValueArchiveRetentionDays = 365,
            HighValueArchiveRetentionDays = 730,
        };

        var job = NewJob(repo.Object, options);
        await job.RunOnceAsync(CancellationToken.None);

        repo.Verify(r => r.DeleteFromArchiveExceptModulesAsync(
            AuditSeverity.Low, It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.DeleteFromArchiveAsync(
            AuditSeverity.High, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
