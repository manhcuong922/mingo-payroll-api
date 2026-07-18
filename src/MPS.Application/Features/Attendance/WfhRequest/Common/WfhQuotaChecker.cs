using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.WfhRequest.Common;

/// <summary>
/// Enforce quota nội quy WFH:
///  - sum(TotalDays trong tháng đã Approved) + đơn hiện tại ≤ AttendanceSetting.WfhMaxDaysPerMonth (mặc định 4).
/// Throw DomainValidationException nếu vượt.
/// </summary>
public sealed class WfhQuotaChecker
{
    public const decimal DefaultMaxDaysPerMonth = 4m;

    private readonly IWfhRequestRepository _repository;
    private readonly IAttendanceSettingRepository _settingRepository;

    public WfhQuotaChecker(
        IWfhRequestRepository repository,
        IAttendanceSettingRepository settingRepository)
    {
        _repository = repository;
        _settingRepository = settingRepository;
    }

    public async Task EnsureWithinQuotaAsync(
        int employeeInformationId,
        DateOnly startDate,
        DateOnly endDate,
        decimal totalDays,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var setting = await _settingRepository.GetOrCreateDefaultAsync(nowUtc, cancellationToken);
        var quota = setting.WfhMaxDaysPerMonth ?? DefaultMaxDaysPerMonth;
        if (quota <= 0m) return; // 0 hoặc âm = tắt quota

        // Cộng dồn theo từng tháng bị đơn "chạm vào".
        var checkMonths = EnumerateMonths(startDate, endDate).Distinct();
        foreach (var (year, month) in checkMonths)
        {
            var monthStart = new DateOnly(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var alreadyApproved = await _repository.SumApprovedDaysInMonthAsync(
                employeeInformationId, year, month, cancellationToken);

            var requestedInMonth = ComputeDaysInMonth(startDate, endDate, monthStart, monthEnd, totalDays);
            var newTotal = alreadyApproved + requestedInMonth;
            if (newTotal > quota)
            {
                var remaining = quota - alreadyApproved;
                throw new DomainValidationException(
                    $"Vượt quỹ WFH tháng {month:D2}/{year}. "
                    + $"Đã Approved {alreadyApproved:0.#} / {quota:0.#} ngày. "
                    + $"Đơn này chiếm {requestedInMonth:0.#} ngày trong tháng — chỉ còn {(remaining > 0 ? remaining : 0):0.#} ngày.");
            }
        }
    }

    private static IEnumerable<(int Year, int Month)> EnumerateMonths(DateOnly start, DateOnly end)
    {
        var cursor = new DateOnly(start.Year, start.Month, 1);
        while (cursor <= end)
        {
            yield return (cursor.Year, cursor.Month);
            cursor = cursor.AddMonths(1);
        }
    }

    private static decimal ComputeDaysInMonth(
        DateOnly requestStart,
        DateOnly requestEnd,
        DateOnly monthStart,
        DateOnly monthEnd,
        decimal requestTotalDays)
    {
        var start = requestStart > monthStart ? requestStart : monthStart;
        var end = requestEnd < monthEnd ? requestEnd : monthEnd;
        if (end < start) return 0m;

        var rangeDays = end.DayNumber - start.DayNumber + 1;
        var origDays = requestEnd.DayNumber - requestStart.DayNumber + 1;
        if (origDays <= 0) return 0m;
        return requestTotalDays * ((decimal)rangeDays / origDays);
    }
}
