namespace MPS.Domain.Enums.Notifications;

/// <summary>
/// Chế độ giao thông báo cho một plan item / draft (Phase 1 mở kênh Email):
///   • <see cref="Default"/>   — lưu in-app + gửi email nếu có <c>EmailContent</c>.
///   • <see cref="InAppOnly"/> — chỉ lưu in-app (bỏ email dù có EmailContent).
///   • <see cref="EmailOnly"/> — chỉ gửi email (không tạo dòng in-app). Dùng cho N3:
///     người nhận không còn login được (tài khoản khoá / nghỉ việc) — chuông vô nghĩa với họ.
/// </summary>
public enum NotificationDeliveryMode : byte
{
    Default = 0,
    InAppOnly = 1,
    EmailOnly = 2,
}
