namespace MPS.Application.Settings;

public sealed class AttendanceCheckinSettings
{
    public const string SectionName = "AttendanceCheckin";

    /// <summary>Số phút được chấm công VÀO CA sớm trước giờ bắt đầu ca.</summary>
    public int EarlyCheckinMinutes { get; init; } = 60;

    /// <summary>Số phút được chấm công RA CA muộn sau giờ kết thúc ca.</summary>
    public int LateCheckoutMinutes { get; init; } = 360;
}
