using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Application.Common.Interfaces;

/// <summary>
/// BR-23 — Resolve DayType cho 1 WorkDate theo thứ tự ưu tiên:
/// (1) PublicHoliday → (2) PaidLeaveDay → (3) WeeklyRestDay (mặc định CN) → (4) Normal.
/// </summary>
public interface IDayTypeResolver
{
    Task<AttendanceEnums.DayType> ResolveAsync(
        int employeeInformationId,
        DateOnly workDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<DateOnly, AttendanceEnums.DayType>> ResolveRangeAsync(
        int employeeInformationId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Biến thể của <see cref="ResolveRangeAsync"/> chuyên dùng cho <b>guard trước khi
    /// tạo assignment</b> (BatchUpdateShiftAssignments). Khác biệt duy nhất: suy
    /// WeeklyRestDay từ DOW-pattern của lịch sử <c>[addFrom-28, addFrom-1]</c> — KHÔNG
    /// dùng chính khoảng đang add làm cửa sổ dò, tránh vòng luẩn quẩn "chưa có ca ⇒
    /// mọi ngày là rest day ⇒ không tạo được ca". Holiday và PaidLeave dò theo semantic
    /// tuyệt đối như cũ.
    /// </summary>
    Task<IReadOnlyDictionary<DateOnly, AttendanceEnums.DayType>> ResolveGuardRangeAsync(
        int employeeInformationId,
        DateOnly addFrom,
        DateOnly addTo,
        CancellationToken cancellationToken = default);
}
