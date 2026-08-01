using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Notifications;
using MPS.Application.Features.Notifications.Abstractions;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Application.Features.Notifications.Templates;
using MPS.Domain.Entities.Notifications;
using MPS.Domain.Enums.Notifications;
using MPS.Domain.Interfaces.Notifications;

namespace MPS.Infrastructure.Notifications;

/// <summary>
/// Write-side: lưu thông báo vào DB (nguồn sự thật), đẩy realtime + dispatch email (Phase 1) best-effort.
/// Lỗi khi đẩy realtime / gửi email KHÔNG làm hỏng việc lưu in-app — chuông vẫn thấy khi FE fetch lại.
///
/// Chính sách kênh (theo <see cref="NotificationDeliveryMode"/> trên từng draft):
///   • Default   → lưu in-app + gửi email nếu draft có Email content.
///   • InAppOnly → chỉ lưu in-app (bỏ email dù có Email content).
///   • EmailOnly → CHỈ gửi email (không tạo dòng in-app) — dùng cho N3: người nhận không login được.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;
    private readonly IRealtimePush _push;
    private readonly IEmailSender _emailSender;
    private readonly IUserEmailResolver _emailResolver;
    private readonly ILogger<NotificationService> _log;

    public NotificationService(
        INotificationRepository repository,
        IRealtimePush push,
        IEmailSender emailSender,
        IUserEmailResolver emailResolver,
        ILogger<NotificationService> log)
    {
        _repository = repository;
        _push = push;
        _emailSender = emailSender;
        _emailResolver = emailResolver;
        _log = log;
    }

    public Task CreateAsync(NotificationDraft draft, CancellationToken ct = default) =>
        CreateManyAsync(new[] { draft }, ct);

    public async Task CreateManyAsync(IReadOnlyCollection<NotificationDraft> drafts, CancellationToken ct = default)
    {
        if (drafts is null || drafts.Count == 0) return;

        // Tách 2 nhóm: có lưu in-app hay không (EmailOnly bỏ in-app).
        var inAppDrafts = drafts.Where(d => d.DeliveryMode != NotificationDeliveryMode.EmailOnly).ToList();
        var entities = inAppDrafts.Select(ToEntity).ToList();

        if (entities.Count > 0)
        {
            await _repository.AddRangeAsync(entities, ct);
            await PushRealtimeAsync(entities, ct);
        }

        await DispatchEmailAsync(drafts, ct);
    }

    private async Task PushRealtimeAsync(IReadOnlyList<Notification> entities, CancellationToken ct)
    {
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

    private async Task DispatchEmailAsync(IReadOnlyCollection<NotificationDraft> drafts, CancellationToken ct)
    {
        // Chỉ draft có Email content + DeliveryMode cho phép gửi email.
        var emailDrafts = drafts
            .Where(d => d.Email is not null && d.DeliveryMode != NotificationDeliveryMode.InAppOnly)
            .ToList();
        if (emailDrafts.Count == 0) return;

        // Tra email theo lô — tách 2 nhóm theo prefersPersonal để dùng đúng chính sách.
        var normalUserIds = emailDrafts.Where(d => !d.PrefersPersonalEmail).Select(d => d.RecipientUserId).Distinct().ToList();
        var personalUserIds = emailDrafts.Where(d => d.PrefersPersonalEmail).Select(d => d.RecipientUserId).Distinct().ToList();

        IReadOnlyDictionary<int, string> normalMap = normalUserIds.Count == 0
            ? new Dictionary<int, string>()
            : await _emailResolver.ResolveDeliveryEmailsAsync(normalUserIds, prefersPersonal: false, ct);
        IReadOnlyDictionary<int, string> personalMap = personalUserIds.Count == 0
            ? new Dictionary<int, string>()
            : await _emailResolver.ResolveDeliveryEmailsAsync(personalUserIds, prefersPersonal: true, ct);

        foreach (var d in emailDrafts)
        {
            var email = d.PrefersPersonalEmail
                ? personalMap.GetValueOrDefault(d.RecipientUserId)
                : normalMap.GetValueOrDefault(d.RecipientUserId);

            if (string.IsNullOrWhiteSpace(email))
            {
                _log.LogWarning(
                    "Bỏ qua email thông báo cho user #{UserId} (type={Type}) — không tra được địa chỉ email (prefersPersonal={Personal}).",
                    d.RecipientUserId, d.Type, d.PrefersPersonalEmail);
                continue;
            }

            try
            {
                await _emailSender.SendGenericNotificationAsync(
                    toEmail: email,
                    subject: d.Email!.Subject,
                    textBody: d.Email.TextBody,
                    htmlBody: d.Email.HtmlBody,
                    ct);
            }
            catch (Exception ex)
            {
                // Không rethrow — email fail không ảnh hưởng in-app đã lưu.
                _log.LogError(ex,
                    "Ghi outbox email thông báo cho user #{UserId} (type={Type}) thất bại (in-app không bị ảnh hưởng).",
                    d.RecipientUserId, d.Type);
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
