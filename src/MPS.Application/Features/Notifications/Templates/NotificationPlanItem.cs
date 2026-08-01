using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates;

/// <summary>
/// Một "mảnh" kế hoạch gửi: NHÓM NGƯỜI NHẬN (spec) + NỘI DUNG cho nhóm đó.
/// Một template có thể trả về nhiều plan item khi cần nội dung khác nhau cho nhóm khác nhau
/// (vd người được giao việc nhận "Bạn có việc mới", quản lý nhận "Đã giao việc cho X").
///
/// Phase 1 mở kênh email:
///   • <see cref="Email"/> — nếu != null, mỗi người nhận sẽ được gửi thêm email.
///   • <see cref="DeliveryMode"/> — mặc định lưu in-app + gửi email; đổi thành
///     <see cref="NotificationDeliveryMode.EmailOnly"/> khi người nhận không còn login được (N3).
///   • <see cref="PrefersPersonalEmail"/> — ưu tiên email cá nhân (EmployeeInformation.Email)
///     trước email công ty (User.Email). Dùng với tài khoản khoá / nghỉ việc.
/// </summary>
public sealed record NotificationPlanItem(
    RecipientSpec Recipients,
    NotificationContent Content,
    EmailContent? Email = null,
    NotificationDeliveryMode DeliveryMode = NotificationDeliveryMode.Default,
    bool PrefersPersonalEmail = false);
