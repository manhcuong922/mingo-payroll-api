using MediatR;
using MPS.Application.Common.Models;

namespace MPS.Application.Features.Auth.Commands;

public sealed record RefreshTokenCommand(
    string RefreshToken,
    string? ClientIp) : IRequest<AuthTokenPair>;
