using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.BackgroundJobs;

/// <summary>
/// Chạy mỗi đêm UTC: activate pending compliance versions có ValidFrom &lt;= today,
/// deactivate version cũ đang active cùng loại.
/// AllowanceExemptThresholdVersion không xử lý ở đây — HR bắt buộc dùng applyImmediately=true.
/// </summary>
public sealed class ComplianceActivationJob : BackgroundService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ComplianceActivationJob> _logger;

    public ComplianceActivationJob(IServiceScopeFactory scopeFactory, ILogger<ComplianceActivationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ComplianceActivationJob started.");

        // Chờ 1 phút khi startup để hệ thống ổn định
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1); // nửa đêm UTC tiếp theo
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
                _logger.LogError(ex, "ComplianceActivationJob tick failed; retry next midnight.");
            }
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogDebug("ComplianceActivationJob already running; skipping.");
            return 0;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

            var today = DateOnly.FromDateTime(clock.UtcNow);
            var totalActivated = 0;

            totalActivated += await ActivateForTypeAsync<Domain.Entities.Compliance.SocialInsuranceRateVersion>(
                db, clock, publisher, today, "SocialInsuranceRateVersion", cancellationToken);
            totalActivated += await ActivateForTypeAsync<Domain.Entities.Compliance.PitRegulationVersion>(
                db, clock, publisher, today, "PitRegulationVersion", cancellationToken);
            totalActivated += await ActivateForTypeAsync<Domain.Entities.Compliance.MinimumWageVersion>(
                db, clock, publisher, today, "MinimumWageVersion", cancellationToken);
            totalActivated += await ActivateForTypeAsync<Domain.Entities.Compliance.LaborLawConstantsVersion>(
                db, clock, publisher, today, "LaborLawConstantsVersion", cancellationToken);

            // Phase 5 §N9-style — cảnh báo pending sẽ active trong T-7 / T-1.
            await PublishPendingSoonAsync<Domain.Entities.Compliance.SocialInsuranceRateVersion>(
                db, publisher, today, "SocialInsuranceRateVersion", "Tỷ lệ BHXH", cancellationToken);
            await PublishPendingSoonAsync<Domain.Entities.Compliance.PitRegulationVersion>(
                db, publisher, today, "PitRegulationVersion", "Thuế TNCN", cancellationToken);
            await PublishPendingSoonAsync<Domain.Entities.Compliance.MinimumWageVersion>(
                db, publisher, today, "MinimumWageVersion", "Lương tối thiểu vùng", cancellationToken);
            await PublishPendingSoonAsync<Domain.Entities.Compliance.LaborLawConstantsVersion>(
                db, publisher, today, "LaborLawConstantsVersion", "Hằng số luật lao động", cancellationToken);

            if (totalActivated > 0)
                _logger.LogInformation("ComplianceActivationJob: activated {Total} version(s) total.", totalActivated);
            else
                _logger.LogDebug("ComplianceActivationJob: no pending versions to activate.");

            return totalActivated;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<int> ActivateForTypeAsync<TVersion>(
        AppDbContext db,
        IClock clock,
        IPublisher publisher,
        DateOnly today,
        string typeName,
        CancellationToken ct)
        where TVersion : class, IComplianceVersion
    {
        var toActivate = await db.Set<TVersion>()
            .Where(x => !x.IsActive && !x.IsArchived && x.ValidFrom <= today)
            .OrderByDescending(x => x.ValidFrom)
            .FirstOrDefaultAsync(ct);

        if (toActivate is null) return 0;

        var current = await db.Set<TVersion>()
            .Where(x => x.IsActive && !x.IsArchived)
            .FirstOrDefaultAsync(ct);

        if (current is not null)
        {
            current.IsActive = false;
            current.UpdatedAt = clock.UtcNow;
        }

        toActivate.IsActive = true;
        toActivate.UpdatedAt = clock.UtcNow;

        await db.SaveChangesAsync(ct);

        await publisher.Publish(new Application.Common.Models.Compliance.ComplianceVersionChangedNotification(typeName), ct);

        _logger.LogInformation(
            "ComplianceActivationJob: activated {Type} id={Id} ValidFrom={ValidFrom:yyyy-MM-dd}.",
            typeName, toActivate.Id, toActivate.ValidFrom);

        return 1;
    }

    private static readonly int[] SoonThresholds = { 7, 1 };

    /// <summary>
    /// Publish <c>CompliancePendingSoonNotification</c> khi pending còn đúng 7 hoặc 1 ngày tới ValidFrom.
    /// Idempotency: publish theo ngày — cùng ngày quét lại vẫn phát (bridge best-effort, không gây hại).
    /// </summary>
    private static async Task PublishPendingSoonAsync<TVersion>(
        AppDbContext db, IPublisher publisher, DateOnly today, string typeName, string displayName, CancellationToken ct)
        where TVersion : class, IComplianceVersion
    {
        foreach (var days in SoonThresholds)
        {
            var target = today.AddDays(days);
            var pending = await db.Set<TVersion>()
                .Where(x => !x.IsActive && !x.IsArchived && x.ValidFrom == target)
                .ToListAsync(ct);

            foreach (var v in pending)
            {
                await publisher.Publish(new Application.Common.Models.Compliance.CompliancePendingSoonNotification(
                    ComplianceType: typeName,
                    DisplayName: displayName,
                    EffectiveFrom: v.ValidFrom.ToDateTime(TimeOnly.MinValue),
                    DaysRemaining: days), ct);
            }
        }
    }
}
