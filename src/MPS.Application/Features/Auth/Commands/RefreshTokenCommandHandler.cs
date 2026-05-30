using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Auth.Commands;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthTokenPair>
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPermissionResolver _permissionResolver;

    public RefreshTokenCommandHandler(
        IRefreshTokenService refreshTokenService,
        IJwtTokenService jwtTokenService,
        IPermissionResolver permissionResolver)
    {
        _refreshTokenService = refreshTokenService;
        _jwtTokenService = jwtTokenService;
        _permissionResolver = permissionResolver;
    }

    public async Task<AuthTokenPair> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var rotation = await _refreshTokenService.RotateAsync(request.RefreshToken, request.ClientIp, cancellationToken);
        var user = rotation.User;

        if (user.Status != 1)
            throw new ForbiddenAccessException("User account is disabled.");

        var permissions = await _permissionResolver.GetPermissionsForRoleAsync(user.RoleId, cancellationToken);
        var accessToken = _jwtTokenService.GenerateAccessToken(user, permissions);

        return new AuthTokenPair(accessToken, rotation.NewRefreshToken);
    }
}
