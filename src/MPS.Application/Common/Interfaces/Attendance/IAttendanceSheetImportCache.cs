namespace MPS.Application.Common.Interfaces.Attendance;

/// <summary>
/// Cache tạm giữa Preview và Confirm của import bảng chấm công. TTL ~15 phút.
/// Key format: userId + previewId (Guid) — chống cross-user access.
/// </summary>
public interface IAttendanceSheetImportCache
{
    void Set(string previewId, AttendanceSheetImportCacheEntry entry, TimeSpan lifetime);
    bool TryGet(string previewId, out AttendanceSheetImportCacheEntry? entry);
    void Remove(string previewId);
}

/// <summary>
/// Entry cache 1 phiên preview. <see cref="ResolvedCells"/> = danh sách ô đã resolve xong (empId + shiftId + workDate + newSymbol) sẵn sàng để Confirm apply.
/// </summary>
public sealed record AttendanceSheetImportCacheEntry(
    int CreatedByUserId,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<AttendanceSheetImportResolvedCell> ResolvedCells);

/// <summary>Ô đã resolve (đủ thông tin để upsert). ChangeType = "create" hoặc "update".</summary>
public sealed record AttendanceSheetImportResolvedCell(
    int EmployeeInformationId,
    string EmployeeCode,
    string EmployeeName,
    DateOnly WorkDate,
    int ShiftTemplateId,
    string ShiftCode,
    string NewSymbol,
    string? OldSymbol,
    string ChangeType);
