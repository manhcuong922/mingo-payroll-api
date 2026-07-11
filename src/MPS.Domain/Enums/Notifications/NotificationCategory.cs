namespace MPS.Domain.Enums.Notifications;

/// <summary>
/// Nhóm thông báo — dùng để FE gom tab/lọc (Tất cả / Công việc / Hợp đồng ...).
/// </summary>
public enum NotificationCategory
{
    System = 0,
    Task = 1,
    Contract = 2,
    Leave = 3,
    Compliance = 4,
    Schedule = 5,
    Payroll = 6,
}
