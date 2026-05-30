using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Domain.Entities;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Infrastructure.Security;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private const int RawTokenBytes = 64;

    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly JwtSettings _settings;

    public RefreshTokenService(
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        IOptions<JwtSettings> options)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _settings = options.Value;
    }

    public async Task<RefreshTokenResult> IssueAsync(User user, string? createdByIp, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var raw = GenerateRawToken();
        var entity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(raw),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenDays),
            CreatedByIp = createdByIp,
        };

        await _refreshTokenRepository.AddAsync(entity, cancellationToken);
        return new RefreshTokenResult(raw, entity.ExpiresAt);
    }

    public async Task<RefreshTokenRotationResult> RotateAsync(string presentedToken, string? requestIp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(presentedToken))
            throw new ForbiddenAccessException("Refresh token is required.");

        var presentedHash = HashToken(presentedToken);
        var stored = await _refreshTokenRepository.GetByTokenHashAsync(presentedHash, cancellationToken);

        if (stored is null)
            throw new ForbiddenAccessException("Refresh token is invalid.");

        if (stored.RevokedAt is not null)
        {
            await _refreshTokenRepository.RevokeAllForUserAsync(
                stored.UserId,
                "Refresh token reuse detected.",
                requestIp,
                cancellationToken);

            throw new ForbiddenAccessException("Refresh token reuse detected. All sessions revoked.");
        }

        if (DateTime.UtcNow >= stored.ExpiresAt)
            throw new ForbiddenAccessException("Refresh token has expired.");

        var user = stored.User
            ?? await _userRepository.GetByIdAsync(stored.UserId, cancellationToken)
            ?? throw new ForbiddenAccessException("User no longer exists.");

        var newRaw = GenerateRawToken();
        var newHash = HashToken(newRaw);
        var now = DateTime.UtcNow;

        var newEntity = new RefreshToken
        {
            UserId = stored.UserId,
            TokenHash = newHash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_settings.RefreshTokenDays),
            CreatedByIp = requestIp,
        };

        await _refreshTokenRepository.AddAsync(newEntity, cancellationToken);

        stored.RevokedAt = now;
        stored.RevokedReason = "Rotated";
        stored.RevokedByIp = requestIp;
        stored.ReplacedByTokenHash = newHash;
        await _refreshTokenRepository.UpdateAsync(stored, cancellationToken);

        return new RefreshTokenRotationResult(user, new RefreshTokenResult(newRaw, newEntity.ExpiresAt));
    }

    public async Task<bool> RevokeAsync(string presentedToken, string reason, string? requestIp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(presentedToken))
            return false;

        var stored = await _refreshTokenRepository.GetByTokenHashAsync(HashToken(presentedToken), cancellationToken);
        if (stored is null || stored.RevokedAt is not null)
            return false;

        stored.RevokedAt = DateTime.UtcNow;
        stored.RevokedReason = reason;
        stored.RevokedByIp = requestIp;
        await _refreshTokenRepository.UpdateAsync(stored, cancellationToken);
        return true;
    }

    public Task RevokeAllForUserAsync(int userId, string reason, string? requestIp, CancellationToken cancellationToken = default) =>
        _refreshTokenRepository.RevokeAllForUserAsync(userId, reason, requestIp, cancellationToken);

    private static string GenerateRawToken()
    {
        Span<byte> buffer = stackalloc byte[RawTokenBytes];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string HashToken(string raw)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
