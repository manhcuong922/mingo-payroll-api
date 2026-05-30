using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RefreshToken>> GetActiveTokensByUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<int> AddAsync(RefreshToken entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(RefreshToken entity, CancellationToken cancellationToken = default);
    Task RevokeAllForUserAsync(int userId, string reason, string? revokedByIp, CancellationToken cancellationToken = default);
}
