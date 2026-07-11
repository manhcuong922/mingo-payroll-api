using MediatR;

namespace MPS.Application.Common.Models.Notifications;

/// <summary>
/// Sự kiện: nội quy công ty được thêm mới / chỉnh sửa / tải phiên bản mới.
/// Module CompanyPolicy chỉ publish sự kiện mỏng này; toàn bộ logic thông báo (ai nhận, nội dung)
/// nằm ở module Notification (template + handler). Đây là hợp đồng chung, không tạo phụ thuộc ngược.
/// </summary>
public sealed record CompanyPolicyChangedNotification(
    int PolicyId,
    string Title,
    CompanyPolicyChangeKind Kind) : INotification;

public enum CompanyPolicyChangeKind
{
    Created = 0,
    Updated = 1,
    NewVersion = 2,
}
