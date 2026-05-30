using MediatR;
using MPS.Application.Common.Models;

namespace MPS.Application.Features.Auth.Commands;

public sealed record LoginCommand(
    string Username,
    string Password,
    string? ClientIp) : IRequest<AuthTokenPair>;
