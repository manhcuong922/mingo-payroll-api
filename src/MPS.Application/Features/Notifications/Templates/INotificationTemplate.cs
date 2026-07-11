using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates;

/// <summary>
/// Hợp đồng cho MỘT loại thông báo. Mỗi loại = 1 file implement (thường kế thừa
/// <see cref="NotificationTemplate{TPayload}"/>). Template tự quyết định NGƯỜI NHẬN + NỘI DUNG
/// từ payload của sự kiện. Được INotifier tra theo <see cref="PayloadType"/>.
///
/// KHÔNG đăng ký DI thủ công — AddNotificationModule tự quét & đăng ký mọi template trong assembly.
/// </summary>
public interface INotificationTemplate
{
    /// <summary>Loại thông báo template này tạo ra.</summary>
    NotificationType Type { get; }

    /// <summary>Kiểu payload mà template này xử lý (dùng để INotifier định tuyến).</summary>
    Type PayloadType { get; }

    /// <summary>Sinh kế hoạch gửi (người nhận + nội dung) từ payload.</summary>
    Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(object payload, CancellationToken ct = default);
}
