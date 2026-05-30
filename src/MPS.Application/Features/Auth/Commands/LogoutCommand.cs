using MediatR;

namespace MPS.Application.Features.Auth.Commands;

public sealed record LogoutCommand(
    string RefreshToken,
    string? ClientIp) : IRequest<bool>;
