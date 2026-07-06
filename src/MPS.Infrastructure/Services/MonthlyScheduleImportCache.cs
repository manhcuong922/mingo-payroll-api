using Microsoft.Extensions.Caching.Memory;
using MPS.Application.Common.Interfaces;

namespace MPS.Infrastructure.Services;

public sealed class MonthlyScheduleImportCache : IMonthlyScheduleImportCache
{
    private const string KeyPrefix = "monthly-schedule-import:";

    private readonly IMemoryCache _cache;

    public MonthlyScheduleImportCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Set(string token, MonthlyScheduleImportCacheEntry entry, TimeSpan lifetime) =>
        _cache.Set(BuildKey(token), entry, lifetime);

    public bool TryGet(string token, out MonthlyScheduleImportCacheEntry? entry)
    {
        if (_cache.TryGetValue(BuildKey(token), out MonthlyScheduleImportCacheEntry? value) && value is not null)
        {
            entry = value;
            return true;
        }
        entry = null;
        return false;
    }

    public void Remove(string token) => _cache.Remove(BuildKey(token));

    private static string BuildKey(string token) => KeyPrefix + token;
}
