using Microsoft.Extensions.Caching.Memory;
using MPS.Application.Common.Interfaces.Attendance;

namespace MPS.Infrastructure.Services.AttendanceSheet;

public sealed class AttendanceSheetImportCache : IAttendanceSheetImportCache
{
    private const string KeyPrefix = "attendance-sheet-import:";

    private readonly IMemoryCache _cache;

    public AttendanceSheetImportCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Set(string previewId, AttendanceSheetImportCacheEntry entry, TimeSpan lifetime) =>
        _cache.Set(KeyPrefix + previewId, entry, lifetime);

    public bool TryGet(string previewId, out AttendanceSheetImportCacheEntry? entry)
    {
        if (_cache.TryGetValue(KeyPrefix + previewId, out AttendanceSheetImportCacheEntry? v) && v is not null)
        {
            entry = v;
            return true;
        }
        entry = null;
        return false;
    }

    public void Remove(string previewId) => _cache.Remove(KeyPrefix + previewId);
}
