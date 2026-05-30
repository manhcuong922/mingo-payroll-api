using MPS.Application.Common.Models;
using MPS.Domain.Entities;

namespace MPS.Application.Common.Interfaces;

public interface IRefreshTokenService
{
    Task<RefreshTokenResult> IssueAsync(User user, string? createdByIp, CancellationToken cancellationToken = default);

    Task<RefreshTokenRotationResult> RotateAsync(string presentedToken, string? requestIp, CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(string presentedToken, string reason, string? requestIp, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(int userId, string reason, string? requestIp, CancellationToken cancellationToken = default);
}

public sealed record RefreshTokenRotationResult(
    User User,
    RefreshTokenResult NewRefreshToken);
