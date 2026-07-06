using MPS.Application.Features.Attendance.EmployeeShift.Commands;

namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Lưu tạm kết quả parse + resolve giữa preview và confirm của import bảng phân ca.
/// Backed bởi IMemoryCache trong Infrastructure; TTL ngắn (~10 phút).
/// </summary>
public interface IMonthlyScheduleImportCache
{
    void Set(string token, MonthlyScheduleImportCacheEntry entry, TimeSpan lifetime);
    bool TryGet(string token, out MonthlyScheduleImportCacheEntry? entry);
    void Remove(string token);
}

public sealed record MonthlyScheduleImportCacheEntry(
    int CreatedByUserId,
    string? ScheduleName,
    int? Year,
    int? Month,
    IReadOnlyList<BatchShiftOperation> Operations);
