using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Interfaces;

namespace MPS.Infrastructure.BackgroundJobs;

/// <summary>
/// Daily tick — 2 việc:
/// <list type="number">
/// <item><b>Promote</b> HĐ đang <c>PendingTermination</c> có TerminationDate đã qua (theo giờ VN)
/// sang <c>Terminated</c>.</item>
/// <item><b>Deactivate</b> tài khoản của NV có bản ghi chấm dứt tới hạn và không còn HĐ nào
/// đang <c>Active</c> hay <c>PendingTermination</c>.</item>
/// </list>
///
/// Semantic ngày: TerminationDate là ngày làm việc CUỐI CÙNG. Job chỉ xử lý khi đã qua ngày đó
/// (giờ VN, UTC+7) — HĐ với TerminationDate = 5/8 chỉ được flip Terminated từ sáng 6/8 VN.
///
/// Luồng HĐ HẾT HẠN tự nhiên có <see cref="ContractExpiryJob"/> lo riêng; job này chỉ phụ trách
/// luồng chấm dứt thủ công.
/// </summary>
public sealed class TerminationAccountDeactivationJob : BackgroundService
{
    private const int VnUtcOffsetHours = 7;

    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TerminationAccountDeactivationJob> _logger;

    public TerminationAccountDeactivationJob(
        IServiceScopeFactory scopeFactory, ILogger<TerminationAccountDeactivationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TerminationAccountDeactivationJob started; interval={Interval}.", Interval);
        try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TerminationAccountDeactivationJob tick failed; retry next interval.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogDebug("TerminationAccountDeactivationJob already running; skipping.");
            return 0;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var terminations = scope.ServiceProvider.GetRequiredService<IContractTerminationRepository>();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            return await RunOnceAsync(terminations, users, clock.UtcNow, _logger, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Lõi thuần (không đụng DI/scope) để test được. Thứ tự BẮT BUỘC:
    /// promote PendingTermination → Terminated trước, rồi mới deactivate account (để check
    /// "còn HĐ Active/PendingTermination" phản ánh đúng trạng thái sau promote).
    /// Trả số tài khoản đã vô hiệu hoá.
    /// </summary>
    public static async Task<int> RunOnceAsync(
        IContractTerminationRepository terminations,
        IUserRepository users,
        DateTime nowUtc,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var todayVn = nowUtc.AddHours(VnUtcOffsetHours).Date;

        var promoted = await terminations.PromoteMaturedPendingTerminationsAsync(todayVn, cancellationToken);
        if (promoted > 0)
            logger.LogInformation(
                "TerminationAccountDeactivationJob: promoted {Count} contract(s) PendingTermination → Terminated.",
                promoted);

        var employeeIds = await terminations.GetEmployeeInformationIdsWithMaturedTerminationAsync(todayVn, cancellationToken);
        if (employeeIds.Count == 0)
        {
            logger.LogDebug("TerminationAccountDeactivationJob: no matured terminations.");
            return 0;
        }

        var deactivated = await users.DeactivateForEmployeesWithoutActiveContractAsync(
            employeeIds, nowUtc, cancellationToken);
        if (deactivated > 0)
            logger.LogInformation(
                "TerminationAccountDeactivationJob: deactivated {Count} user account(s) of terminated employees.",
                deactivated);
        return deactivated;
    }
}
