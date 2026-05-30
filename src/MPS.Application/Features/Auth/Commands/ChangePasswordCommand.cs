using MediatR;

namespace MPS.Application.Features.Auth.Commands;

public sealed record ChangePasswordCommand(
    string OldPassword,
    string NewPassword,
    string? ClientIp) : IRequest<bool>;
