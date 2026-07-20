using MPS.Application.Features.Attendance.EmployeeShift.Commands;
using MPS.Domain.Enums.Attendance;

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

/// <summary>
/// Cache entry cho 1 phiên import.
/// Với <see cref="ScheduleApplyTarget.Departments"/> mode, <see cref="DeptIdByOpIndex"/>
/// map từ index trong <see cref="Operations"/> sang DepartmentId gốc — Confirm handler
/// dùng để group ops theo (ShiftTemplateId, DepartmentId) khi tạo templates.
/// Employees mode: dict null.
/// </summary>
public sealed record MonthlyScheduleImportCacheEntry(
    int CreatedByUserId,
    string? ScheduleName,
    int? Year,
    int? Month,
    IReadOnlyList<BatchShiftOperation> Operations,
    ScheduleApplyTarget ApplyTarget,
    IReadOnlyDictionary<int, int>? DeptIdByOpIndex);
