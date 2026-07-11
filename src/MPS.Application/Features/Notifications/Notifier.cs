using Microsoft.Extensions.Logging;
using MPS.Application.Features.Notifications.Abstractions;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Application.Features.Notifications.Templates;

namespace MPS.Application.Features.Notifications;

/// <summary>
/// Điều phối phát thông báo: payload → template(s) khớp kiểu → plan (người nhận + nội dung)
/// → nở người nhận → tạo draft → INotificationService (lưu DB + đẩy realtime).
///
/// Lỗi phát thông báo KHÔNG được làm hỏng nghiệp vụ chính — bọc try/catch + log.
/// </summary>
public sealed class Notifier : INotifier
{
    private readonly IEnumerable<INotificationTemplate> _templates;
    private readonly IRecipientResolver _recipients;
    private readonly INotificationService _notifications;
    private readonly ILogger<Notifier> _log;

    public Notifier(
        IEnumerable<INotificationTemplate> templates,
        IRecipientResolver recipients,
        INotificationService notifications,
        ILogger<Notifier> log)
    {
        _templates = templates;
        _recipients = recipients;
        _notifications = notifications;
        _log = log;
    }

    public async Task RaiseAsync<TPayload>(TPayload payload, CancellationToken ct = default)
        where TPayload : notnull
    {
        try
        {
            var matching = _templates.Where(t => t.PayloadType == typeof(TPayload)).ToList();
            if (matching.Count == 0)
            {
                _log.LogWarning(
                    "Không có template thông báo nào cho payload {PayloadType} — bỏ qua.",
                    typeof(TPayload).Name);
                return;
            }

            var drafts = new List<NotificationDraft>();

            foreach (var template in matching)
            {
                var plan = await template.BuildAsync(payload, ct);
                foreach (var item in plan)
                {
                    var userIds = await _recipients.ResolveAsync(item.Recipients, ct);
                    foreach (var userId in userIds)
                    {
                        drafts.Add(new NotificationDraft(
                            RecipientUserId: userId,
                            Type: template.Type,
                            Category: item.Content.Category,
                            Severity: item.Content.Severity,
                            Title: item.Content.Title,
                            Message: item.Content.Message,
                            LinkUrl: item.Content.LinkUrl,
                            MetadataJson: item.Content.MetadataJson));
                    }
                }
            }

            if (drafts.Count > 0)
                await _notifications.CreateManyAsync(drafts, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "Phát thông báo cho payload {PayloadType} thất bại (nghiệp vụ chính không bị ảnh hưởng).",
                typeof(TPayload).Name);
        }
    }
}
