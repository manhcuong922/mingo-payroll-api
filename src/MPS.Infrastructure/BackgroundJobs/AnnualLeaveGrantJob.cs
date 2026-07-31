using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.LeaveBalance.Common;

namespace MPS.Infrastructure.BackgroundJobs;

/// <summary>
/// <c>[D22]</c> nửa (a) — **đầu năm tự cấp quỹ phép cho toàn bộ nhân viên**.
///
/// <para><b>Lịch chạy.</b> Tick mỗi 24 giờ; chỉ thực sự cấp khi ngày giờ Việt Nam (UTC+7) đang ở
/// <b>tháng 01</b>. Lấy cả tháng thay vì đúng 01/01 để lượt cấp không bị mất nếu server tắt đúng
/// ngày đầu năm — chạy lại trong tháng vẫn ra đúng số vì phép tính là hàm thuần của (ngày vào làm,
/// khoảng HĐ, năm) và <c>UsedDays</c> luôn được giữ nguyên.</para>
///
/// <para><b>Trigger tay:</b> <c>POST /api/admin/leave-balances/jobs/annual-grant/run</c> — dùng cho
/// vận hành (cấp bù sau khi sửa dữ liệu HĐ) và cho E2E.</para>
///
/// <para>Toàn bộ nghiệp vụ nằm ở <see cref="IAnnualLeaveGrantService"/> (tầng Application, unit-test
/// được); job này chỉ lo lịch + scope DI, giống các job hợp đồng.</para>
/// </summary>
public sealed class AnnualLeaveGrantJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    /// <summary>Tháng cấp quỹ đầu năm (tháng 01).</summary>
    private const int GrantMonth = 1;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnnualLeaveGrantJob> _logger;

    public AnnualLeaveGrantJob(IServiceScopeFactory scopeFactory, ILogger<AnnualLeaveGrantJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AnnualLeaveGrantJob started; interval={Interval}.", Interval);
        try { await Task.Delay(TimeSpan.FromMinutes(4), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AnnualLeaveGrantJob tick failed; retry next interval.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        var vietnamNow = DateTime.UtcNow.Add(VietnamOffset);
        if (vietnamNow.Month != GrantMonth)
        {
            _logger.LogDebug(
                "AnnualLeaveGrantJob: ngoài kỳ cấp quỹ đầu năm (đang tháng {Month}); bỏ qua lượt này.",
                vietnamNow.Month);
            return;
        }

        await RunOnceAsync(vietnamNow.Year, null, cancellationToken);
    }

    /// <summary>
    /// Chạy một lượt cấp quỹ cho <paramref name="year"/>. <paramref name="onlyEmployeeInformationId"/>
    /// khác null ⇒ chỉ cấp cho một NV (cấp bù sau khi sửa hồ sơ/HĐ). Trả null nếu đang có lượt khác
    /// chạy dở — không xếp hàng chờ.
    /// </summary>
    public async Task<AnnualLeaveGrantResult?> RunOnceAsync(
        int year, int? onlyEmployeeInformationId, CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogDebug("AnnualLeaveGrantJob already running; skipping.");
            return null;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var granter = scope.ServiceProvider.GetRequiredService<IAnnualLeaveGrantService>();
            var systemUserId = await ResolveSystemActorAsync(scope.ServiceProvider, cancellationToken);

            return await granter.GrantForYearAsync(
                year, systemUserId, onlyEmployeeInformationId, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Job không có người dùng đăng nhập ⇒ ghi <c>CreatedByUserId</c> là tài khoản quản trị hệ
    /// thống để bản ghi vẫn truy được nguồn gốc.
    /// </summary>
    private static async Task<int> ResolveSystemActorAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var users = services.GetRequiredService<MPS.Domain.Interfaces.IUserRepository>();
        var admin = await users.GetByUsernameAsync("admin", cancellationToken);
        return admin?.Id ?? 1;
    }
}
