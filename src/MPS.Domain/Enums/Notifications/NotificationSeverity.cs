namespace MPS.Domain.Enums.Notifications;

/// <summary>
/// Mức độ thông báo — FE dùng để chọn màu/icon (info xanh, warning vàng, critical đỏ...).
/// </summary>
public enum NotificationSeverity
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Critical = 3,
}
