using MPS.Domain.Entities.Notifications;

namespace MPS.Domain.Interfaces.Notifications;

/// <summary>
/// Truy cập dữ liệu thông báo (bounded context riêng — NotificationDbContext).
/// </summary>
public interface INotificationRepository
{
    /// <summary>Thêm nhiều thông báo (broadcast nhiều người nhận trong 1 sự kiện) và lưu.</summary>
    Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken ct = default);

    /// <summary>Danh sách thông báo của 1 người nhận, mới nhất trước (paged).</summary>
    Task<(IReadOnlyList<Notification> Items, int Total)> GetPagedByRecipientAsync(
        int recipientUserId,
        bool? isRead,
        int page,
        int size,
        CancellationToken ct = default);

    /// <summary>Đếm số thông báo chưa đọc (badge chuông).</summary>
    Task<int> CountUnreadAsync(int recipientUserId, CancellationToken ct = default);

    /// <summary>Đánh dấu 1 thông báo đã đọc. Trả về false nếu không thuộc người nhận / không tồn tại.</summary>
    Task<bool> MarkReadAsync(Guid id, int recipientUserId, CancellationToken ct = default);

    /// <summary>Đánh dấu tất cả thông báo chưa đọc của người nhận là đã đọc. Trả về số bản ghi cập nhật.</summary>
    Task<int> MarkAllReadAsync(int recipientUserId, CancellationToken ct = default);
}
