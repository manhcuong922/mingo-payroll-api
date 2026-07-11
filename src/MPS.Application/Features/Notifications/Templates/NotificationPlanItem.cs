using MPS.Application.Features.Notifications.Recipients;

namespace MPS.Application.Features.Notifications.Templates;

/// <summary>
/// Một "mảnh" kế hoạch gửi: NHÓM NGƯỜI NHẬN (spec) + NỘI DUNG cho nhóm đó.
/// Một template có thể trả về nhiều plan item khi cần nội dung khác nhau cho nhóm khác nhau
/// (vd người được giao việc nhận "Bạn có việc mới", quản lý nhận "Đã giao việc cho X").
/// </summary>
public sealed record NotificationPlanItem(RecipientSpec Recipients, NotificationContent Content);
