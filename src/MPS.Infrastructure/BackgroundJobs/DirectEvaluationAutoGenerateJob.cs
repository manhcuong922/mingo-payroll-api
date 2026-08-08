using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Evaluation.Common;
using MPS.Domain.Common;

namespace MPS.Infrastructure.BackgroundJobs;

/// <summary>
/// Auto-sinh bảng đánh giá tháng Direct (Loại B) với ĐIỂM MẶC ĐỊNH cho tổ trưởng / Planning Manager /
/// kế toán / HR, để Director không phải tạo tay từng người từng tháng.
///
/// <para><b>Lịch chạy.</b> Tick mỗi 24 giờ; mỗi lượt sinh cho <b>tháng liền trước</b> theo giờ Việt Nam
/// (ví dụ tick ngày bất kỳ trong tháng 09 ⇒ sinh kỳ 08). Chấm sau khi tháng đã kết thúc mới đúng
/// nghiệp vụ, và cùng nhịp với việc HR bấm "Tổng hợp tháng" cho công nhân. Chạy lại là an toàn:
/// service bỏ qua người đã có bảng tháng, nên server tắt ngày 01 thì lượt hôm sau vẫn bù đủ.</para>
///
/// <para><b>Trigger tay:</b> <c>POST /api/MonthlyEvaluation/direct/auto-generate</c> (quyền
/// <c>MonthlyEvaluation.ScoreDirect.All</c>) — dùng khi cần sinh bù sau lúc sửa rubric, và cho E2E.</para>
///
/// <para>Toàn bộ nghiệp vụ nằm ở <see cref="IDirectEvaluationAutoGenerateService"/> (tầng Application,
/// unit-test được); job này chỉ lo lịch + scope DI, giống <see cref="AnnualLeaveGrantJob"/>.</para>
/// </summary>
public sealed class DirectEvaluationAutoGenerateJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DirectEvaluationAutoGenerateJob> _logger;

    public DirectEvaluationAutoGenerateJob(
        IServiceScopeFactory scopeFactory, ILogger<DirectEvaluationAutoGenerateJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DirectEvaluationAutoGenerateJob started; interval={Interval}.", Interval);
        try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DirectEvaluationAutoGenerateJob tick failed; retry next interval.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        var previousMonth = VietnamClock.Now.AddMonths(-1);
        await RunOnceAsync(previousMonth.Year, previousMonth.Month, cancellationToken);
    }

    /// <summary>
    /// Chạy một lượt sinh bảng cho kỳ (<paramref name="year"/>, <paramref name="month"/>).
    /// Trả null nếu đang có lượt khác chạy dở — không xếp hàng chờ.
    /// </summary>
    public async Task<DirectEvaluationAutoGenerateResult?> RunOnceAsync(
        int year, int month, CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogDebug("DirectEvaluationAutoGenerateJob already running; skipping.");
            return null;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var generator = scope.ServiceProvider.GetRequiredService<IDirectEvaluationAutoGenerateService>();
            var systemUserId = await ResolveSystemActorAsync(scope.ServiceProvider, cancellationToken);

            return await generator.GenerateForMonthAsync(year, month, systemUserId, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Job không có người dùng đăng nhập ⇒ ghi <c>CreatedByUserId</c> là tài khoản quản trị hệ thống
    /// để bản ghi vẫn truy được nguồn gốc.
    /// </summary>
    private static async Task<int> ResolveSystemActorAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var users = services.GetRequiredService<MPS.Domain.Interfaces.IUserRepository>();
        var admin = await users.GetByUsernameAsync("admin", cancellationToken);
        return admin?.Id ?? 1;
    }
}
