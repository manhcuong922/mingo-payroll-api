using MediatR;

namespace MPS.Application.Features.Auth.Commands;

public sealed record ResetPasswordCommand(
    string Token,
    string NewPassword,
    string? ClientIp) : IRequest<bool>;
