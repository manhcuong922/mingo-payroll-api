namespace MPS.Application.Settings;

public sealed class AttendanceCheckinSettings
{
    public const string SectionName = "AttendanceCheckin";

    /// <summary>Số phút được chấm công VÀO CA sớm trước giờ bắt đầu ca.</summary>
    public int EarlyCheckinMinutes { get; init; } = 60;

    /// <summary>
    /// Số phút được chấm công RA CA muộn sau giờ kết thúc ca.
    /// <para>⚠️ KHÔNG giảm xuống dưới ~300: OT trần 4h/ngày (<c>LaborLawConstantsVersion.OvertimeMaxHoursPerDay</c>)
    /// cộng nghỉ giữa ca ⇒ NV tan ca sau OT có thể quét muộn hơn giờ tan ca chính vài tiếng.
    /// Cắt ngắn con số này là cắt luôn giờ OT thực tế mà <c>FaceCheckinCommandHandler.RecordActualOvertimeIfAnyAsync</c>
    /// ghi nhận được.</para>
    /// </summary>
    public int LateCheckoutMinutes { get; init; } = 360;

    /// <summary>
    /// Số phút được chấm công VÀO CA muộn sau giờ bắt đầu ca — hạn chót MỞ ca mới.
    /// <para>Tách riêng khỏi <see cref="LateCheckoutMinutes"/>: trước đây hai việc dùng chung một
    /// cửa sổ, nên quét lần đầu lúc 20:00 cho ca 08:00–17:00 vẫn MỞ được ca (tạo bản ghi check-in
    /// lúc 20:00) — ra <c>HalfDay</c> = 0.5 công hưởng lương dù giờ công thực tế bằng 0
    /// (<c>AttendanceHourCalculator.ClampToShiftWindow</c> cắt sạch phần ngoài khung ca).</para>
    /// <para>⚠️ KHÔNG đặt thấp hơn 240: đó là trần <c>LateEarlyRequest.Minutes</c> — NV có đơn xin
    /// đi trễ ĐÃ DUYỆT tối đa 4 tiếng là hợp lệ, hạ cap xuống sẽ chặn nhầm chính họ.</para>
    /// <para>Hạn chót thực tế luôn bị kẹp thêm bởi giờ TAN ca: chấm vào sau khi ca đã kết thúc thì
    /// không còn ý nghĩa, kể cả với ca ngắn hơn 4 tiếng.</para>
    /// </summary>
    public int LateCheckinMinutes { get; init; } = 240;
}
