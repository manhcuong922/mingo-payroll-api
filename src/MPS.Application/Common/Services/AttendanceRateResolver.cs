using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Common.Services;

/// <summary>
/// Resolve hệ số công theo (ruleId, workDate, dayType) — dùng cho attendance summary.
/// Prefetch versions của các rule liên quan 1 lần trước khi gọi Resolve trong loop record để tránh N+1.
/// Fallback 1.0m nếu không tìm được rule hoặc không có version phủ workDate.
/// </summary>
public interface IAttendanceRateResolver
{
    Task PrefetchAsync(IEnumerable<int> ruleIds, CancellationToken cancellationToken);
    decimal Resolve(int? ruleId, DateOnly workDate, DayType dayType);
}

public sealed class AttendanceRateResolver : IAttendanceRateResolver
{
    private readonly IAttendanceRuleVersionRepository _versionRepository;
    private readonly Dictionary<int, List<AttendanceRuleVersion>> _cache = new();

    public AttendanceRateResolver(IAttendanceRuleVersionRepository versionRepository)
    {
        _versionRepository = versionRepository;
    }

    public async Task PrefetchAsync(IEnumerable<int> ruleIds, CancellationToken cancellationToken)
    {
        var missing = ruleIds.Distinct().Where(id => !_cache.ContainsKey(id)).ToList();
        if (missing.Count == 0) return;

        var map = await _versionRepository.GetByRuleIdsAsync(missing, cancellationToken);
        foreach (var id in missing)
        {
            _cache[id] = map.TryGetValue(id, out var list)
                ? list.OrderBy(v => v.EffectiveFrom).ToList()
                : new List<AttendanceRuleVersion>();
        }
    }

    public decimal Resolve(int? ruleId, DateOnly workDate, DayType dayType)
    {
        if (ruleId is null) return 1.0m;

        if (!_cache.TryGetValue(ruleId.Value, out var versions) || versions.Count == 0)
            return 1.0m;

        // Version phủ workDate: EffectiveFrom <= workDate <= (EffectiveTo ?? +∞).
        var version = versions.LastOrDefault(v =>
            v.EffectiveFrom <= workDate && (v.EffectiveTo == null || v.EffectiveTo >= workDate));

        if (version is null) return 1.0m;

        return dayType switch
        {
            DayType.PublicHoliday => version.WorkRateHoliday,
            DayType.WeeklyRestDay => version.WorkRateWeeklyRest,
            _ => version.WorkRateNormal,
        };
    }
}
