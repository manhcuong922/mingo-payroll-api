using MediatR;
using MPS.Application.Common.Interfaces;

namespace MPS.Application.Features.User.Commands;

/// <summary>
/// Delegate thẳng cho <see cref="IFirstLoginInvitationService"/> — service tự invalidate token cũ,
/// tạo token mới, gửi mail. Handler chỉ để endpoint đi qua MediatR nhất quán với các command khác.
/// </summary>
public sealed class ResendFirstLoginInvitationCommandHandler
    : IRequestHandler<ResendFirstLoginInvitationCommand, DateTime>
{
    private readonly IFirstLoginInvitationService _firstLoginInvitation;

    public ResendFirstLoginInvitationCommandHandler(IFirstLoginInvitationService firstLoginInvitation)
    {
        _firstLoginInvitation = firstLoginInvitation;
    }

    public Task<DateTime> Handle(ResendFirstLoginInvitationCommand request, CancellationToken cancellationToken)
        => _firstLoginInvitation.IssueAsync(request.UserId, cancellationToken);
}
