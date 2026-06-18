using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MPS.Application.Options;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;

namespace MPS.Infrastructure.BackgroundJobs;

/// <summary>
/// Daily UTC midnight job — chia 3 bước, mỗi bước transaction riêng để 1 bước fail
/// không kéo bước khác:
///  1. Move active → archive (global cutoff = ActiveRetentionDays).
///  2. Xóa Low cũ trong archive — 2a per-module override, 2b sweep default.
///  3. Xóa High cũ trong archive (chỉ chạy nếu HighValueArchiveRetentionDays != null).
/// </summary>
public sealed class AuditLogRetentionJob : BackgroundService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogRetentionJob> _logger;

    public AuditLogRetentionJob(IServiceScopeFactory scopeFactory, ILogger<AuditLogRetentionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AuditLogRetentionJob started.");

        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1);
            var delay = nextRun - now;

            try { await Task.Delay(delay, stoppingToken); }
            catch (TaskCanceledException) { break; }

            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AuditLogRetentionJob tick failed; retry next midnight.");
            }
        }
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogDebug("AuditLogRetentionJob already running; skipping.");
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
            var options = scope.ServiceProvider.GetRequiredService<IOptions<AuditLogOptions>>().Value;

            var nowUtc = DateTime.UtcNow;

            await Step1MoveAsync(repo, options, nowUtc, cancellationToken);
            await Step2DeleteLowAsync(repo, options, nowUtc, cancellationToken);
            await Step3DeleteHighAsync(repo, options, nowUtc, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task Step1MoveAsync(
        IAuditLogRepository repo,
        AuditLogOptions options,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var cutoff = nowUtc.AddDays(-options.ActiveRetentionDays);
        try
        {
            var moved = await repo.MoveOlderThanAsync(cutoff, ct);
            if (moved > 0)
                _logger.LogInformation("AuditLogRetentionJob step1: moved {Count} rows to archive (cutoff={Cutoff:O}).", moved, cutoff);
            else
                _logger.LogDebug("AuditLogRetentionJob step1: no rows to move.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuditLogRetentionJob step1 (move active→archive) failed.");
        }
    }

    private async Task Step2DeleteLowAsync(
        IAuditLogRepository repo,
        AuditLogOptions options,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var overriddenModules = new List<string>(options.LowValueArchiveRetentionByModule.Keys);

        // 2a — per-module overrides
        foreach (var (module, days) in options.LowValueArchiveRetentionByModule)
        {
            var cutoff = nowUtc.AddDays(-days);
            try
            {
                var deleted = await repo.DeleteFromArchiveForModuleAsync(AuditSeverity.Low, module, cutoff, ct);
                if (deleted > 0)
                    _logger.LogInformation(
                        "AuditLogRetentionJob step2a: deleted {Count} Low rows for module {Module} (cutoff={Cutoff:O}).",
                        deleted, module, cutoff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AuditLogRetentionJob step2a failed for module {Module}; continuing with next module/step.",
                    module);
            }
        }

        // 2b — sweep default cho phần còn lại
        try
        {
            var cutoff = nowUtc.AddDays(-options.LowValueArchiveRetentionDays);
            var deleted = await repo.DeleteFromArchiveExceptModulesAsync(AuditSeverity.Low, overriddenModules, cutoff, ct);
            if (deleted > 0)
                _logger.LogInformation(
                    "AuditLogRetentionJob step2b: deleted {Count} Low rows (default sweep, cutoff={Cutoff:O}).",
                    deleted, cutoff);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuditLogRetentionJob step2b (sweep default) failed.");
        }
    }

    private async Task Step3DeleteHighAsync(
        IAuditLogRepository repo,
        AuditLogOptions options,
        DateTime nowUtc,
        CancellationToken ct)
    {
        if (options.HighValueArchiveRetentionDays is not int days)
        {
            _logger.LogDebug("AuditLogRetentionJob step3: HighValueArchiveRetentionDays is null; skipping.");
            return;
        }

        try
        {
            var cutoff = nowUtc.AddDays(-days);
            var deleted = await repo.DeleteFromArchiveAsync(AuditSeverity.High, cutoff, ct);
            if (deleted > 0)
                _logger.LogInformation(
                    "AuditLogRetentionJob step3: deleted {Count} High rows (cutoff={Cutoff:O}).",
                    deleted, cutoff);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuditLogRetentionJob step3 (delete High) failed.");
        }
    }
}
