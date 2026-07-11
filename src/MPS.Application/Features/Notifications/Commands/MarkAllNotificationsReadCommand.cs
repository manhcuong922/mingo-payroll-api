using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Notifications;

namespace MPS.Application.Features.Notifications.Commands;

/// <summary>Đánh dấu tất cả thông báo chưa đọc của user hiện tại là đã đọc. Trả về số bản ghi cập nhật.</summary>
public sealed record MarkAllNotificationsReadCommand : IRequest<int>;

public sealed class MarkAllNotificationsReadCommandHandler
    : IRequestHandler<MarkAllNotificationsReadCommand, int>
{
    private readonly INotificationRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public MarkAllNotificationsReadCommandHandler(
        INotificationRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public Task<int> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        return _repository.MarkAllReadAsync(userId, cancellationToken);
    }
}
