namespace MPS.Application.Common.Services;

/// <summary>
/// ⭐ RESTDAY-2026-08 — NGÀY NGHỈ HẰNG TUẦN là thứ do doanh nghiệp ẤN ĐỊNH trong nội quy lao động
/// (Điều 111 BLLĐ), KHÔNG suy ra từ việc hôm đó nhân viên có được xếp ca hay không.
///
/// <para><b>Vì sao đổi.</b> Bản A2 trước đây suy ngày nghỉ tuần từ <c>EmployeeShiftAssignment</c>:
/// ngày không có bản phân ca ⇒ ngày nghỉ tuần. Suy như vậy sai ở CẢ HAI chiều:</para>
/// <list type="bullet">
///   <item><b>Trả THIẾU</b> — công ty làm T2–T6, HR xếp một ca Thứ Bảy (trực, làm bù) thì Thứ Bảy
///     đó lập tức thành ngày làm việc bình thường: ca chính chỉ được 100% thay vì 200%
///     (Đ.98.1b) và OT hôm đó ăn 150% thay vì 200%. Mất đúng một ngày lương mỗi lần.</item>
///   <item><b>Trả THỪA</b> — lịch phân ca mới xếp nửa tháng thì nửa còn lại (T2–T6 thật) bị coi là
///     ngày nghỉ tuần; chấm công những ngày đó ăn phụ trội 100% oan. Chấm công KHÔNG đòi phải có
///     bản phân ca (xem <c>RecordAttendanceCommandHandler</c>) nên lỗ hổng này có thật.</item>
/// </list>
///
/// <para>Cấu hình là <b>bitmask theo <see cref="DayOfWeek"/></b> — bit N bật ⇔ thứ N là ngày nghỉ
/// hằng tuần. Chủ nhật = bit 0 … Thứ Bảy = bit 6, khớp đúng giá trị enum của .NET nên không cần
/// bảng quy đổi. Xưởng làm T2–T7 khai mask = chỉ Chủ nhật, giữ nguyên ý định của A2 mà không phải
/// đoán qua roster.</para>
///
/// <para>Mask <c>null</c> (chưa cấu hình / kỳ dữ liệu cũ) ⇒ <see cref="DefaultMask"/> = T7 + CN,
/// đúng quy ước mà A2 vẫn dùng làm fallback khi chưa số hoá lịch phân ca.</para>
/// </summary>
public static class WeeklyRestDayPolicy
{
    /// <summary>Thứ Bảy + Chủ nhật — mặc định khi chưa ai cấu hình.</summary>
    public const int DefaultMask = (1 << (int)DayOfWeek.Sunday) | (1 << (int)DayOfWeek.Saturday);

    /// <summary>Cả 7 thứ — giá trị lớn nhất về mặt bit, nhưng KHÔNG hợp lệ (xem <see cref="IsValid"/>).</summary>
    public const int AllDaysMask = 0b111_1111;

    /// <summary>Mask hợp lệ: ít nhất 1 ngày nghỉ (Đ.111) và ít nhất 1 ngày làm việc trong tuần.</summary>
    public static bool IsValid(int mask) => mask > 0 && mask < AllDaysMask;

    /// <summary><c>true</c> nếu <paramref name="date"/> rơi vào ngày nghỉ hằng tuần theo <paramref name="mask"/>.</summary>
    public static bool IsRestDay(DateOnly date, int? mask) =>
        ((mask ?? DefaultMask) & (1 << (int)date.DayOfWeek)) != 0;

    /// <summary>Liệt kê các thứ đang được khai là ngày nghỉ — dùng cho thông báo lỗi và UI.</summary>
    public static IEnumerable<DayOfWeek> Days(int? mask)
    {
        var effective = mask ?? DefaultMask;
        for (var d = 0; d < 7; d++)
            if ((effective & (1 << d)) != 0)
                yield return (DayOfWeek)d;
    }
}
