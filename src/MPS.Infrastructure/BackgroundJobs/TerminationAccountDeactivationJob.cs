using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Interfaces;

namespace MPS.Infrastructure.BackgroundJobs;

/// <summary>
/// Case 7 — Daily tick: NV có HĐ đã CHẤM DỨT thủ công (ContractTermination) tới hạn
/// (TerminationDate &lt;= now) và không còn HĐ Active nào ⇒ vô hiệu hoá tài khoản.
///
/// Lỗ hổng trước bản vá: luồng chấm dứt chỉ đặt Contract.Status=Terminated, KHÔNG đụng tài khoản —
/// nên NV nghỉ việc vẫn đăng nhập/thao tác được. Luồng HĐ HẾT HẠN đã có <see cref="ContractExpiryJob"/>
/// deactivate; job này bù cho luồng chấm dứt.
///
/// Tôn trọng thời gian báo trước: chấm dứt có thể đặt TerminationDate ở tương lai, khi đó tài khoản
/// vẫn giữ hoạt động tới khi qua mốc. Việc "không còn HĐ Active" do
/// <see cref="IUserRepository.DeactivateForEmployeesWithoutActiveContractAsync"/> tự lọc (NV còn HĐ
/// Active khác — vd tái ký — sẽ được bỏ qua).
/// </summary>
public sealed class TerminationAccountDeactivationJob : BackgroundService
{
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

            return await DeactivateMaturedAsync(terminations, users, clock.UtcNow, _logger, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Lõi thuần (không đụng DI/scope) để test được: lấy NV có chấm dứt tới hạn → deactivate account
    /// những người không còn HĐ Active. Trả số tài khoản đã vô hiệu hoá.
    /// </summary>
    public static async Task<int> DeactivateMaturedAsync(
        IContractTerminationRepository terminations,
        IUserRepository users,
        DateTime nowUtc,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var employeeIds = await terminations.GetEmployeeInformationIdsWithMaturedTerminationAsync(nowUtc, cancellationToken);
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
