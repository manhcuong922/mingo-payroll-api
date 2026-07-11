using MPS.Application.Features.Notifications.Abstractions;
using MPS.Application.Features.Notifications.DTOs;

namespace MPS.Infrastructure.Notifications.Realtime;

/// <summary>
/// Impl no-op cho Phase 1 (chưa có transport realtime). Toàn bộ luồng ghi vẫn chạy;
/// chuông hoạt động kiểu polling. Phase 2 thay bằng SignalRRealtimePush qua 1 dòng đăng ký DI.
/// </summary>
public sealed class NullRealtimePush : IRealtimePush
{
    public Task PushAsync(int recipientUserId, NotificationDto notification, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task PushUnreadCountAsync(int recipientUserId, int unreadCount, CancellationToken ct = default)
        => Task.CompletedTask;
}
