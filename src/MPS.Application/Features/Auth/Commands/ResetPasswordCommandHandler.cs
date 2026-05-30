using System.Security.Cryptography;
using System.Text;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Auth.Commands;

public sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, bool>
{
    private const string RevokeReason = "Password reset";

    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenService _refreshTokenService;

    public ResetPasswordCommandHandler(
        IPasswordResetTokenRepository tokenRepository,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IRefreshTokenService refreshTokenService)
    {
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<bool> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(request.Token);
        var stored = await _tokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken)
            ?? throw new ForbiddenAccessException("Reset token is invalid.");

        if (stored.UsedAt is not null)
            throw new ForbiddenAccessException("Reset token has already been used.");

        if (DateTime.UtcNow >= stored.ExpiresAt)
            throw new ForbiddenAccessException("Reset token has expired.");

        var user = stored.User
            ?? await _userRepository.GetByIdAsync(stored.UserId, cancellationToken)
            ?? throw new ForbiddenAccessException("User no longer exists.");

        if (user.Status != 1)
            throw new ForbiddenAccessException("User account is disabled.");

        var newHash = _passwordHasher.Hash(request.NewPassword);
        var updated = await _userRepository.UpdatePasswordAsync(user.Id, newHash, cancellationToken);
        if (!updated) return false;

        await _tokenRepository.MarkUsedAsync(stored.Id, DateTime.UtcNow, cancellationToken);
        await _refreshTokenService.RevokeAllForUserAsync(user.Id, RevokeReason, request.ClientIp, cancellationToken);
        return true;
    }

    private static string HashToken(string raw)
    {
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
