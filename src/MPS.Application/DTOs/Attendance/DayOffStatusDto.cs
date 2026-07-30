namespace MPS.Application.DTOs.Attendance;

/// <summary>
/// Hôm nay nhân viên có nghỉ không — dùng cho kiosk chấm công (dashboard hiển thị
/// thông báo và chặn vào trang quét khuôn mặt) lẫn guard phía server ở luồng face check-in.
/// </summary>
/// <param name="Reason">
/// <see cref="DayOffReasons"/>: Leave / PublicHoliday / WeeklyRest. Null khi không phải ngày nghỉ.
/// </param>
/// <param name="Message">Câu tiếng Việt hiển thị thẳng lên UI. Null khi không phải ngày nghỉ.</param>
/// <param name="DetailName">Tên ngày lễ hoặc tên loại nghỉ phép, để UI hiển thị chi tiết hơn.</param>
public sealed record DayOffStatusDto(
    DateOnly Date,
    bool IsDayOff,
    string? Reason,
    string? Message,
    string? DetailName);

/// <summary>Các giá trị hợp lệ của <see cref="DayOffStatusDto.Reason"/>.</summary>
public static class DayOffReasons
{
    public const string Leave = "Leave";
    public const string PublicHoliday = "PublicHoliday";
    public const string WeeklyRest = "WeeklyRest";
}
