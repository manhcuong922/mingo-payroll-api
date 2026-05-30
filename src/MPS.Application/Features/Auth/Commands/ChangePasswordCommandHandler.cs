using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Auth.Commands;

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, bool>
{
    private const string RevokeReason = "Password changed";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ICurrentUserService _currentUserService;

    public ChangePasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IRefreshTokenService refreshTokenService,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _refreshTokenService = refreshTokenService;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new ForbiddenAccessException("Authentication required.");

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new ForbiddenAccessException("User not found.");

        if (!_passwordHasher.Verify(request.OldPassword, user.PasswordHash))
            throw new ForbiddenAccessException("Current password is incorrect.");

        var newHash = _passwordHasher.Hash(request.NewPassword);
        var updated = await _userRepository.UpdatePasswordAsync(userId, newHash, cancellationToken);
        if (!updated) return false;

        await _refreshTokenService.RevokeAllForUserAsync(userId, RevokeReason, request.ClientIp, cancellationToken);
        return true;
    }
}
