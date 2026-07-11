using Microsoft.Extensions.Logging;
using MPS.Application.Features.Notifications;
using MPS.Application.Features.Notifications.Abstractions;
using MPS.Domain.Entities.Notifications;
using MPS.Domain.Interfaces.Notifications;

namespace MPS.Infrastructure.Notifications;

/// <summary>
/// Write-side: lưu thông báo vào DB (nguồn sự thật) rồi đẩy realtime (best-effort).
/// Lỗi khi đẩy realtime KHÔNG làm hỏng việc lưu — chuông vẫn thấy khi FE fetch lại.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;
    private readonly IRealtimePush _push;
    private readonly ILogger<NotificationService> _log;

    public NotificationService(
        INotificationRepository repository,
        IRealtimePush push,
        ILogger<NotificationService> log)
    {
        _repository = repository;
        _push = push;
        _log = log;
    }

    public Task CreateAsync(NotificationDraft draft, CancellationToken ct = default) =>
        CreateManyAsync(new[] { draft }, ct);

    public async Task CreateManyAsync(IReadOnlyCollection<NotificationDraft> drafts, CancellationToken ct = default)
    {
        if (drafts is null || drafts.Count == 0) return;

        var entities = drafts.Select(ToEntity).ToList();
        await _repository.AddRangeAsync(entities, ct);

        // Đẩy realtime cho từng người nhận (best-effort). Gộp badge theo người nhận.
        foreach (var recipientId in entities.Select(e => e.RecipientUserId).Distinct())
        {
            try
            {
                foreach (var entity in entities.Where(e => e.RecipientUserId == recipientId))
                    await _push.PushAsync(recipientId, entity.ToDto(), ct);

                var unread = await _repository.CountUnreadAsync(recipientId, ct);
                await _push.PushUnreadCountAsync(recipientId, unread, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex,
                    "Đẩy realtime thông báo cho user #{RecipientUserId} thất bại (đã lưu DB, FE sẽ đồng bộ khi fetch lại).",
                    recipientId);
            }
        }
    }

    private static Notification ToEntity(NotificationDraft d) => new()
    {
        RecipientUserId = d.RecipientUserId,
        Type = d.Type,
        Category = d.Category,
        Severity = d.Severity,
        Title = d.Title,
        Message = d.Message,
        LinkUrl = d.LinkUrl,
        MetadataJson = d.MetadataJson,
        IsRead = false,
    };
}
