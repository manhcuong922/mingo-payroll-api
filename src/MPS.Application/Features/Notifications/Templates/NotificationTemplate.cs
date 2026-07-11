using MPS.Domain.Enums.Notifications;
using MPS.Application.Features.Notifications.Recipients;

namespace MPS.Application.Features.Notifications.Templates;

/// <summary>
/// Lớp cơ sở tiện dụng cho template gắn với 1 kiểu payload <typeparamref name="TPayload"/>.
/// Lớp con chỉ cần override <see cref="Type"/> và <see cref="BuildAsync(TPayload, CancellationToken)"/>,
/// dùng các helper <see cref="Plan"/> / <see cref="Content"/> để bớt boilerplate.
///
/// Template ĐƯỢC PHÉP inject repository/service (Application interfaces) qua constructor để
/// tra thêm dữ liệu (vd tên đơn, danh sách người trong ca) khi dựng nội dung/người nhận.
/// </summary>
public abstract class NotificationTemplate<TPayload> : INotificationTemplate
{
    public abstract NotificationType Type { get; }

    public Type PayloadType => typeof(TPayload);

    public Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(object payload, CancellationToken ct = default)
        => BuildAsync((TPayload)payload, ct);

    protected abstract Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(TPayload payload, CancellationToken ct);

    // ----- Helpers -----

    /// <summary>Tạo 1 plan item.</summary>
    protected static NotificationPlanItem Plan(RecipientSpec recipients, NotificationContent content)
        => new(recipients, content);

    /// <summary>Bọc 1 plan item thành kết quả (trường hợp phổ biến nhất: 1 nhóm nhận 1 nội dung).</summary>
    protected static Task<IReadOnlyList<NotificationPlanItem>> Single(
        RecipientSpec recipients, NotificationContent content)
        => Task.FromResult<IReadOnlyList<NotificationPlanItem>>(new[] { Plan(recipients, content) });

    /// <summary>Gói nhiều plan item thành kết quả.</summary>
    protected static Task<IReadOnlyList<NotificationPlanItem>> Many(params NotificationPlanItem[] items)
        => Task.FromResult<IReadOnlyList<NotificationPlanItem>>(items);

    /// <summary>Không gửi gì (vd payload không thoả điều kiện).</summary>
    protected static Task<IReadOnlyList<NotificationPlanItem>> Nothing()
        => Task.FromResult<IReadOnlyList<NotificationPlanItem>>(Array.Empty<NotificationPlanItem>());

    /// <summary>Tạo nội dung nhanh với Type đã biết từ template.</summary>
    protected NotificationContent Content(
        NotificationCategory category,
        string title,
        string message,
        string? linkUrl = null,
        NotificationSeverity severity = NotificationSeverity.Info,
        string? metadataJson = null)
        => new(category, severity, title, message, linkUrl, metadataJson);
}
