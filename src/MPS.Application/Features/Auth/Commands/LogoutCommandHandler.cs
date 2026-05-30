using MediatR;
using MPS.Application.Common.Interfaces;

namespace MPS.Application.Features.Auth.Commands;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, bool>
{
    private const string LogoutReason = "User logout";

    private readonly IRefreshTokenService _refreshTokenService;

    public LogoutCommandHandler(IRefreshTokenService refreshTokenService)
    {
        _refreshTokenService = refreshTokenService;
    }

    public Task<bool> Handle(LogoutCommand request, CancellationToken cancellationToken) =>
        _refreshTokenService.RevokeAsync(request.RefreshToken, LogoutReason, request.ClientIp, cancellationToken);
}
