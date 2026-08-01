using MediatR;
using MPS.Application.Common.Interfaces;

namespace MPS.Application.Features.User.Commands;

/// <summary>
/// HR tạo tài khoản mới cho NV: provisioner tạo <c>User</c> (hash 1 password random không dùng)
/// rồi <see cref="IFirstLoginInvitationService"/> gửi link đặt password lần đầu qua email.
/// KHÔNG còn gửi password thô (plan §2 N8 — Hướng B).
/// </summary>
public class UserCreateCommandHandler : IRequestHandler<UserCreateCommand, UserCreateResult>
{
    private readonly IEmployeeAccountProvisioner _provisioner;
    private readonly IFirstLoginInvitationService _firstLoginInvitation;

    public UserCreateCommandHandler(
        IEmployeeAccountProvisioner provisioner,
        IFirstLoginInvitationService firstLoginInvitation)
    {
        _provisioner = provisioner;
        _firstLoginInvitation = firstLoginInvitation;
    }

    public async Task<UserCreateResult> Handle(UserCreateCommand request, CancellationToken cancellationToken)
    {
        var credentials = await _provisioner.ProvisionAsync(
            request.EmployeeInformationId, request.RoleId, cancellationToken);

        await _firstLoginInvitation.IssueAsync(credentials.UserId, cancellationToken);

        return new UserCreateResult(credentials.UserId, credentials.EmployeeCode, credentials.Username);
    }
}
