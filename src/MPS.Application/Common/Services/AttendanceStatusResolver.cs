using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using AttendanceSettingEntity = MPS.Domain.Entities.Attendance.AttendanceSetting;

namespace MPS.Application.Common.Services;

/// <summary>
/// Hardcoded defaults dùng khi <see cref="AttendanceSettingEntity"/> để null các field grace.
/// Đặt trong Application layer để cả resolver và test tham chiếu chung.
/// </summary>
public static class AttendanceGraceDefaults
{
    public const int LateGraceMinutes = 5;
    public const int EarlyLeaveGraceMinutes = 5;
    public const int HalfDayThresholdMinutes = 240; // 4h — nửa ca 8h
}

/// <summary>
/// Suy <see cref="AttendanceStatus"/> từ giờ chấm + shift + setting cho record ca chính.
/// Chỉ dùng khi caller không truyền status thủ công.
/// </summary>
public interface IAttendanceStatusResolver
{
    /// <summary>
    /// Suy <see cref="AttendanceStatus"/> theo thứ tự ưu tiên:
    /// <list type="number">
    ///   <item>Không có check-in → <see cref="AttendanceStatus.Absent"/>.</item>
    ///   <item>Muộn hoặc sớm ≥ HalfDayThreshold → <see cref="AttendanceStatus.HalfDay"/>.</item>
    ///   <item>Vừa muộn vừa sớm (đều vượt grace) → <see cref="AttendanceStatus.HalfDay"/>.</item>
    ///   <item>Chỉ muộn vượt grace → <see cref="AttendanceStatus.Late"/>.</item>
    ///   <item>Chỉ về sớm vượt grace → <see cref="AttendanceStatus.EarlyLeave"/>.</item>
    ///   <item>Còn lại → <see cref="AttendanceStatus.Present"/>.</item>
    /// </list>
    /// <see cref="AttendanceStatus.OnLeave"/> không do resolver quyết — HR chọn thủ công.
    /// </summary>
    AttendanceStatus Resolve(
        DateTime? checkInAt,
        DateTime? checkOutAt,
        ShiftTemplate shift,
        DateOnly workDate,
        AttendanceSettingEntity? setting);
}

public sealed class AttendanceStatusResolver : IAttendanceStatusResolver
{
    public AttendanceStatus Resolve(
        DateTime? checkInAt,
        DateTime? checkOutAt,
        ShiftTemplate shift,
        DateOnly workDate,
        AttendanceSettingEntity? setting)
    {
        ArgumentNullException.ThrowIfNull(shift);

        if (checkInAt is null)
            return AttendanceStatus.Absent;

        // Phép đo dùng CHUNG với WorkDayWeightCalculator — xem ShiftDeviation.
        var (lateMin, earlyMin) = ShiftDeviation.Measure(checkInAt, checkOutAt, shift, workDate);

        var graceLate = setting?.LateGraceMinutes ?? AttendanceGraceDefaults.LateGraceMinutes;
        var graceEarly = setting?.EarlyLeaveGraceMinutes ?? AttendanceGraceDefaults.EarlyLeaveGraceMinutes;
        var halfDayThreshold = setting?.HalfDayThresholdMinutes ?? AttendanceGraceDefaults.HalfDayThresholdMinutes;

        // 1. Muộn hoặc sớm >= HalfDayThreshold → HalfDay.
        if (lateMin >= halfDayThreshold || earlyMin >= halfDayThreshold)
            return AttendanceStatus.HalfDay;

        // 2. Cả muộn lẫn về sớm (đều vượt grace) → cộng dồn xem như HalfDay.
        if (lateMin > graceLate && earlyMin > graceEarly)
            return AttendanceStatus.HalfDay;

        // 3. Chỉ muộn vượt grace → Late.
        if (lateMin > graceLate)
            return AttendanceStatus.Late;

        // 4. Chỉ về sớm vượt grace → EarlyLeave.
        if (earlyMin > graceEarly)
            return AttendanceStatus.EarlyLeave;

        // 5. Còn lại → Present.
        return AttendanceStatus.Present;
    }
}
