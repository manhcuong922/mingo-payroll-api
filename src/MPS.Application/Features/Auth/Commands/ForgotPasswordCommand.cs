using MediatR;

namespace MPS.Application.Features.Auth.Commands;

public sealed record ForgotPasswordCommand(
    string Email,
    string? ClientIp) : IRequest<bool>;
