using MPS.Application.Features.Notifications.DTOs;

namespace MPS.Application.Features.Notifications.Abstractions;

/// <summary>
/// Trừu tượng hoá kênh đẩy realtime (Phase 2 = SignalR). Phase 1 dùng NullRealtimePush (no-op)
/// nên toàn bộ luồng ghi vẫn chạy mà chưa cần transport. Đổi transport = đổi 1 đăng ký DI.
/// </summary>
public interface IRealtimePush
{
    /// <summary>Đẩy 1 thông báo tới đúng người nhận (nếu đang online).</summary>
    Task PushAsync(int recipientUserId, NotificationDto notification, CancellationToken ct = default);

    /// <summary>Báo số chưa đọc mới để FE cập nhật badge chuông.</summary>
    Task PushUnreadCountAsync(int recipientUserId, int unreadCount, CancellationToken ct = default);
}
