using MediatR;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Models.Notifications;
using MPS.Application.Features.Notifications.Abstractions;

namespace MPS.Application.Features.Notifications.EventHandlers;

// Các handler mỏng nối domain event (MediatR) → INotifier. MediatR tự phát hiện & gọi khi event publish.
// Notifier tự bọc try/catch nên các handler này KHÔNG làm hỏng nghiệp vụ nguồn nếu phát thông báo lỗi.
//
// Thêm loại thông báo mới về sau = thêm 1 handler mỏng như dưới đây + 1 template tương ứng.

/// <summary>Luật/quy định thay đổi → phát thông báo (template ComplianceChangedTemplate xử lý).</summary>
public sealed class ComplianceChangedBridge : INotificationHandler<ComplianceVersionChangedNotification>
{
    private readonly INotifier _notifier;
    public ComplianceChangedBridge(INotifier notifier) => _notifier = notifier;

    public Task Handle(ComplianceVersionChangedNotification n, CancellationToken ct) =>
        _notifier.RaiseAsync(n, ct);
}

/// <summary>Nội quy công ty thay đổi → phát thông báo.</summary>
public sealed class CompanyPolicyChangedBridge : INotificationHandler<CompanyPolicyChangedNotification>
{
    private readonly INotifier _notifier;
    public CompanyPolicyChangedBridge(INotifier notifier) => _notifier = notifier;

    public Task Handle(CompanyPolicyChangedNotification n, CancellationToken ct) =>
        _notifier.RaiseAsync(n, ct);
}

/// <summary>Giao đơn/việc cho nhân sự → phát thông báo cho người được giao.</summary>
public sealed class TaskAssignedBridge : INotificationHandler<TaskAssignedDomainNotification>
{
    private readonly INotifier _notifier;
    public TaskAssignedBridge(INotifier notifier) => _notifier = notifier;

    public Task Handle(TaskAssignedDomainNotification n, CancellationToken ct) =>
        _notifier.RaiseAsync(n, ct);
}
