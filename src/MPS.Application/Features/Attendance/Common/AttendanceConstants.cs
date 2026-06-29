namespace MPS.Application.Features.Attendance.Common;

/// <summary>
/// Hằng số attendance — khung giờ đêm theo Điều 106 BLLĐ (22h–6h).
///
/// TODO §10: chuyển 2 giá trị này (cùng với <c>WorkRateNight</c> floor và các cap
/// Điều 105/107) vào <c>LaborLawConstantsVersion</c> để versioned + HR cập nhật
/// được qua UI khi luật sửa.
/// </summary>
public static class AttendanceConstants
{
    public static readonly TimeOnly NightStart = new(22, 0);
    public static readonly TimeOnly NightEnd   = new(6, 0);
}
