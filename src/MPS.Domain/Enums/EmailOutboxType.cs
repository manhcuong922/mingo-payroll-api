namespace MPS.Domain.Enums;

/// <summary>
/// Loại email trong outbox — quyết định cách worker dựng nội dung khi gửi.
/// </summary>
public enum EmailOutboxType : byte
{
    PasswordReset = 1,

    /// <summary>
    /// [DEPRECATED — sẽ xoá khi mọi caller di dời sang <see cref="FirstLoginInvitation"/>.]
    /// Email chứa password thô. Không còn caller nào phát nữa (đã chuyển sang link đặt password).
    /// GIÁ TRỊ 2 giữ nguyên trong enum để dữ liệu outbox cũ không rơi vào nhánh unknown khi worker chạy.
    /// </summary>
    AccountCredentials = 2,

    ContractActivation = 3,

    /// <summary>
    /// Mail mời đặt mật khẩu lần đầu (Hướng B — plan §2 N8): chứa 1 link 1-lần hết hạn sau X giờ,
    /// dẫn tới form đặt password. Thay thế hoàn toàn <see cref="AccountCredentials"/> cho tài khoản mới.
    /// </summary>
    FirstLoginInvitation = 4,

    /// <summary>
    /// Email chung do NotificationService phát sinh — payload chỉ chứa Subject/TextBody/HtmlBody
    /// đã render sẵn ở template thông báo. KHÔNG dùng cho email nghiệp vụ cần cấu trúc riêng
    /// (VD attach PDF hợp đồng — dùng ContractActivation). Dải 99 dành riêng cho fallback.
    /// </summary>
    GenericNotification = 99,
}
