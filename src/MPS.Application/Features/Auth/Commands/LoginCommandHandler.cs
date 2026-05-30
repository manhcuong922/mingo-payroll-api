using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Auth.Commands;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthTokenPair>
{
    private const string InvalidCredentialsMessage = "Invalid username or password.";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IPermissionResolver _permissionResolver;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        IPermissionResolver permissionResolver)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _permissionResolver = permissionResolver;
    }

    public async Task<AuthTokenPair> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByUsernameAsync(request.Username, cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new ForbiddenAccessException(InvalidCredentialsMessage);

        if (user.Status != 1)
            throw new ForbiddenAccessException("User account is disabled.");

        var permissions = await _permissionResolver.GetPermissionsForRoleAsync(user.RoleId, cancellationToken);
        var accessToken = _jwtTokenService.GenerateAccessToken(user, permissions);
        var refreshToken = await _refreshTokenService.IssueAsync(user, request.ClientIp, cancellationToken);

        await _userRepository.UpdateLastLoginAsync(user.Id, DateTime.UtcNow, cancellationToken);

        return new AuthTokenPair(accessToken, refreshToken);
    }
}
