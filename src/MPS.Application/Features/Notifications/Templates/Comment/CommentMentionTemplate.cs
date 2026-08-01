using MPS.Application.Common.Models.Notifications;
using MPS.Application.Features.Notifications.Recipients;
using MPS.Domain.Enums.Notifications;

namespace MPS.Application.Features.Notifications.Templates.Comment;

/// <summary>
/// Phase 6 — @mention trong Comment: chỉ in-app (không gửi email theo mặc định để tránh spam
/// high-frequency). Tương lai có bảng NotificationPreferences → cho user tự bật email nếu muốn.
/// </summary>
public sealed class CommentMentionTemplate : NotificationTemplate<CommentMentionNotification>
{
    public override NotificationType Type => NotificationType.CommentMention;

    protected override Task<IReadOnlyList<NotificationPlanItem>> BuildAsync(
        CommentMentionNotification e, CancellationToken ct)
    {
        var snippet = e.CommentSnippet.Length <= 120
            ? e.CommentSnippet
            : e.CommentSnippet[..120] + "…";

        var content = Content(
            category: NotificationCategory.System,
            title: "Bạn được nhắc trong bình luận",
            message: snippet,
            linkUrl: e.LinkUrl);

        return Single(
            RecipientSpec.Users(e.MentionedUserId).Excluding(e.MentionedByUserId),
            content);
    }
}
