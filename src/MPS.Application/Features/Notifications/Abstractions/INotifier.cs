namespace MPS.Application.Features.Notifications.Abstractions;

/// <summary>
/// Điểm vào DUY NHẤT để phát thông báo từ nghiệp vụ. Event handler (Phase 3) hoặc bất kỳ
/// module nào chỉ cần gọi:
/// <code>await _notifier.RaiseAsync(new ShiftCreatedPayload(...), ct);</code>
/// Notifier tự: tìm template theo kiểu payload → dựng người nhận + nội dung → lưu DB → đẩy realtime.
///
/// Người gọi KHÔNG cần biết ai nhận / nội dung gì — toàn bộ nằm trong template tương ứng.
/// </summary>
public interface INotifier
{
    Task RaiseAsync<TPayload>(TPayload payload, CancellationToken ct = default) where TPayload : notnull;
}
