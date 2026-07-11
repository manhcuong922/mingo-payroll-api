using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Application.Features.Notifications.DTOs;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Notifications;

namespace MPS.Application.Features.Notifications.Queries;

/// <summary>
/// Lấy danh sách thông báo của người dùng hiện tại (paged, mới nhất trước).
/// isRead = null: tất cả; true: đã đọc; false: chưa đọc.
/// </summary>
public sealed record GetMyNotificationsQuery(bool? IsRead, int Page, int Size)
    : IRequest<PagedResult<NotificationDto>>;

public sealed class GetMyNotificationsQueryHandler
    : IRequestHandler<GetMyNotificationsQuery, PagedResult<NotificationDto>>
{
    private const int MaxSize = 100;

    private readonly INotificationRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetMyNotificationsQueryHandler(
        INotificationRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<NotificationDto>> Handle(
        GetMyNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        var page = request.Page < 1 ? 1 : request.Page;
        var size = request.Size < 1 ? 20 : Math.Min(request.Size, MaxSize);

        var (items, total) = await _repository.GetPagedByRecipientAsync(
            userId, request.IsRead, page, size, cancellationToken);

        return new PagedResult<NotificationDto>(
            items.Select(n => n.ToDto()).ToList(),
            total,
            page,
            size);
    }
}
