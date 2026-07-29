using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Interfaces.Payroll;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Services;

/// <summary>
/// Đánh cờ stale bằng set-based ExecuteUpdate (không nạp entity) — rẻ và chạy được từ nhiều luồng
/// nguồn khác nhau. Luôn ghi đè lý do + mốc thời gian gần nhất để HR thấy nguồn đổi cuối cùng.
///
/// Đây là side-effect PHỤ của thao tác nguồn (duyệt đơn, sửa công…) và chạy SAU khi thao tác chính
/// đã lưu. Lỗi ở đây KHÔNG được làm hỏng nghiệp vụ chính nên được nuốt + log tại chỗ (worst case:
/// kỳ không được đánh cờ = đúng như trước khi có tính năng này, không phải regression).
/// </summary>
public sealed class PayrollStaleMarker : IPayrollStaleMarker
{
    private const int ReasonMaxLength = 500;

    private readonly AppDbContext _context;
    private readonly ILogger<PayrollStaleMarker> _logger;

    public PayrollStaleMarker(AppDbContext context, ILogger<PayrollStaleMarker> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task MarkStaleForMonthAsync(int year, int month, string reason, CancellationToken ct = default) =>
        MarkAsync(p => p.PeriodYear == year && p.PeriodMonth == month, reason, ct);

    public async Task MarkStaleForMonthsAsync(
        IEnumerable<(int Year, int Month)> months, string reason, CancellationToken ct = default)
    {
        var distinct = months.Distinct().ToList();
        foreach (var (year, month) in distinct)
            await MarkStaleForMonthAsync(year, month, reason, ct);
    }

    public Task MarkStaleFromMonthAsync(int year, int month, string reason, CancellationToken ct = default)
    {
        // (PeriodYear, PeriodMonth) >= (year, month) — so sánh theo year*12+month để đúng thứ tự tháng.
        var floor = year * 12 + month;
        return MarkAsync(p => p.PeriodYear * 12 + p.PeriodMonth >= floor, reason, ct);
    }

    // [D27] — không lọc tháng: band hệ số áp cho TOÀN BỘ NV nên đổi band làm mọi kỳ Draft cần tính lại.
    public Task MarkAllStaleAsync(string reason, CancellationToken ct = default) =>
        MarkAsync(_ => true, reason, ct);

    private async Task MarkAsync(
        System.Linq.Expressions.Expression<Func<Domain.Entities.Payroll.PayrollPeriod, bool>> scope,
        string reason,
        CancellationToken ct)
    {
        var trimmed = reason.Length <= ReasonMaxLength ? reason : reason[..ReasonMaxLength];
        var nowUtc = DateTime.UtcNow;

        try
        {
            await _context.PayrollPeriods
                .Where(p => p.Status == PayrollPeriodStatus.Draft)
                .Where(scope)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.IsStale, true)
                    .SetProperty(p => p.StaleReason, trimmed)
                    .SetProperty(p => p.StaleMarkedAt, nowUtc), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không đánh được cờ stale cho kỳ lương ({Reason}).", trimmed);
        }
    }
}
