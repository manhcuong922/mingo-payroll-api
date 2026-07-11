using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Notifications;

namespace MPS.Application.Features.Notifications.Commands;

/// <summary>Đánh dấu 1 thông báo (của chính user hiện tại) là đã đọc.</summary>
public sealed record MarkNotificationReadCommand(Guid Id) : IRequest<bool>;

public sealed class MarkNotificationReadCommandHandler
    : IRequestHandler<MarkNotificationReadCommand, bool>
{
    private readonly INotificationRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public MarkNotificationReadCommandHandler(
        INotificationRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public Task<bool> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        return _repository.MarkReadAsync(request.Id, userId, cancellationToken);
    }
}
