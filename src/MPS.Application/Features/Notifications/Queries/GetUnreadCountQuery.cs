using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Notifications;

namespace MPS.Application.Features.Notifications.Queries;

/// <summary>Đếm số thông báo chưa đọc của người dùng hiện tại (badge chuông).</summary>
public sealed record GetUnreadCountQuery : IRequest<int>;

public sealed class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, int>
{
    private readonly INotificationRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetUnreadCountQueryHandler(
        INotificationRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public Task<int> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        return _repository.CountUnreadAsync(userId, cancellationToken);
    }
}
