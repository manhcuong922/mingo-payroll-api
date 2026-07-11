namespace MPS.Application.Features.Notifications.Abstractions;

/// <summary>
/// Cửa ngõ ghi thông báo (write-side). Lưu DB (nguồn sự thật) rồi đẩy realtime qua IRealtimePush.
/// Dùng bởi các event handler / INotifier ở Phase 3. Tách khỏi read-side (queries) để rõ trách nhiệm.
/// </summary>
public interface INotificationService
{
    /// <summary>Tạo 1 thông báo.</summary>
    Task CreateAsync(NotificationDraft draft, CancellationToken ct = default);

    /// <summary>Tạo nhiều thông báo (broadcast nhiều người nhận trong 1 sự kiện) trong 1 lần lưu.</summary>
    Task CreateManyAsync(IReadOnlyCollection<NotificationDraft> drafts, CancellationToken ct = default);
}
