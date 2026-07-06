namespace MPS.Application.Features.Attendance.OvertimeSlotTemplate.Common;

/// <summary>
/// Mức sàn hệ số OT theo BLLĐ 2019 (Điều 98) + Thông tư 18/2021/TT-BLĐTBXH.
/// Đơn vị: phần trăm (%). Ví dụ 150m nghĩa là 150%.
/// Nếu override trên <see cref="Domain.Entities.Attendance.OvertimeSlotTemplate"/> null → composer dùng sàn.
/// Nếu có override → validator ép giá trị phải &gt;= sàn tương ứng.
/// </summary>
public static class OvertimeMultiplierFloors
{
    // Điều 98.1: OT ban ngày
    public const decimal NormalDay      = 150m;
    public const decimal WeeklyRestDay  = 200m;
    public const decimal HolidayDay     = 300m;

    // Điều 98.3 + Thông tư 18/2021: OT ban đêm
    public const decimal NormalNightNoPrevOt   = 200m;
    public const decimal NormalNightWithPrevOt = 210m;
    public const decimal WeeklyRestNight       = 270m;
    public const decimal HolidayNight          = 390m;
}
