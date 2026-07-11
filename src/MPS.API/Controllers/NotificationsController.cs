using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MPS.Application.Features.Notifications.Commands;
using MPS.Application.Features.Notifications.Queries;

namespace MPS.API.Controllers;

/// <summary>
/// Thông báo in-app (chuông). Chỉ thao tác trên thông báo của CHÍNH user hiện tại —
/// không cần permission code riêng (dữ liệu cá nhân), chỉ cần đã đăng nhập.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Danh sách thông báo của tôi (paged, mới nhất trước). isRead: null=tất cả, true/false=lọc.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(
        [FromQuery] bool? isRead,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMyNotificationsQuery(isRead, page, size), cancellationToken);
        return Ok(result);
    }

    /// <summary>Số thông báo chưa đọc (badge chuông).</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var count = await _mediator.Send(new GetUnreadCountQuery(), cancellationToken);
        return Ok(new { unreadCount = count });
    }

    /// <summary>Đánh dấu 1 thông báo đã đọc.</summary>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var ok = await _mediator.Send(new MarkNotificationReadCommand(id), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    /// <summary>Đánh dấu tất cả thông báo là đã đọc. Trả về số bản ghi cập nhật.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new MarkAllNotificationsReadCommand(), cancellationToken);
        return Ok(new { updated });
    }
}
