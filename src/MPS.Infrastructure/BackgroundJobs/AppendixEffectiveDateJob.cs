using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.ContractAppendix.Common;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Payroll;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.BackgroundJobs;

/// <summary>
/// Daily tick: các phụ lục <c>Scheduled</c> có <c>EffectiveFrom &lt;= NOW</c> → apply changes lên HĐ
/// và promote sang <c>Active</c>. Mỗi phụ lục xử lý trong 1 transaction riêng nên lỗi 1 cái không
/// block cả batch. Sau khi promote thành công mới trigger payroll invalidation.
/// </summary>
public sealed class AppendixEffectiveDateJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppendixEffectiveDateJob> _logger;

    public AppendixEffectiveDateJob(IServiceScopeFactory scopeFactory, ILogger<AppendixEffectiveDateJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppendixEffectiveDateJob started; interval={Interval}.", Interval);
        try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AppendixEffectiveDateJob tick failed; retry next interval.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogDebug("AppendixEffectiveDateJob already running; skipping.");
            return 0;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var repo = scope.ServiceProvider.GetRequiredService<IContractAppendixRepository>();
            var applier = scope.ServiceProvider.GetRequiredService<AppendixApplier>();
            var payrollChange = scope.ServiceProvider.GetRequiredService<IPayrollSourceChangeHandler>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var nowUtc = clock.UtcNow;
            var dueIds = await repo.GetDueScheduledAppendixIdsAsync(nowUtc, cancellationToken);
            if (dueIds.Count == 0)
            {
                _logger.LogDebug("AppendixEffectiveDateJob: no scheduled appendices due.");
                return 0;
            }

            var promoted = 0;
            foreach (var appendixId in dueIds)
            {
                try
                {
                    var change = await LoadChangeSetAsync(db, appendixId, cancellationToken);
                    if (change is null) continue;

                    await repo.PromoteScheduledAsync(
                        appendixId,
                        (contract, allowances, appendix) =>
                            applier.ApplyAsync(contract, allowances, appendix, change, nowUtc, cancellationToken),
                        cancellationToken);

                    var promotedAppendix = await db.ContractAppendices.AsNoTracking()
                        .Include(a => a.Contract)
                        .FirstAsync(a => a.Id == appendixId, cancellationToken);

                    await payrollChange.OnEffectiveFromChangedAsync(
                        promotedAppendix.EffectiveFrom.Year, promotedAppendix.EffectiveFrom.Month,
                        promotedAppendix.Contract.EmployeeInformationId,
                        $"Promote phụ lục HĐ {promotedAppendix.AppendixNo} (hiệu lực {promotedAppendix.EffectiveFrom:dd/MM/yyyy})",
                        cancellationToken);

                    promoted++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "AppendixEffectiveDateJob: failed to promote appendix {AppendixId}; skipping to next.",
                        appendixId);
                }
            }

            _logger.LogInformation("AppendixEffectiveDateJob: promoted {Count}/{Total} scheduled appendices.",
                promoted, dueIds.Count);
            return promoted;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<AppendixChangeSet?> LoadChangeSetAsync(
        AppDbContext db, int appendixId, CancellationToken ct)
    {
        var json = await db.ContractAppendices.AsNoTracking()
            .Where(a => a.Id == appendixId)
            .Select(a => a.ChangedClausesJson)
            .FirstOrDefaultAsync(ct);
        return json is null ? null : AppendixSchemaValidator.ParseAndValidate(json);
    }
}
