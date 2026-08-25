using AttendanceEnums = MPS.Domain.Enums.Attendance;

namespace MPS.Application.Common.Interfaces;

/// <summary>
/// BR-23 — Resolve DayType cho 1 WorkDate theo thứ tự ưu tiên:
/// (1) PublicHoliday → (2) PaidLeaveDay → (3) WeeklyRestDay → (4) Normal.
/// Ngày nghỉ hằng tuần đọc từ cấu hình <c>AttendanceSetting.WeeklyRestDayMask</c> (mặc định T7 + CN).
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
    /// Biến thể của <see cref="ResolveRangeAsync"/> dùng cho <b>guard trước khi tạo assignment</b>
    /// (xếp ca, đơn nghỉ phép, đơn OT, đơn WFH).
    ///
    /// <para>⭐ RESTDAY-2026-08 — nay cho kết quả ĐỒNG NHẤT với <see cref="ResolveRangeAsync"/>.
    /// Bản cũ phải dò DOW-pattern của <c>[addFrom-28, addFrom-1]</c> để thoát vòng luẩn quẩn "chưa
    /// có ca ⇒ mọi ngày là rest day ⇒ không tạo được ca"; vòng đó sinh ra từ chính việc suy ngày
    /// nghỉ tuần từ roster, nay ngày nghỉ tuần đọc thẳng từ cấu hình nên không còn.</para>
    /// </summary>
    Task<IReadOnlyDictionary<DateOnly, AttendanceEnums.DayType>> ResolveGuardRangeAsync(
        int employeeInformationId,
        DateOnly addFrom,
        DateOnly addTo,
        CancellationToken cancellationToken = default);
}
