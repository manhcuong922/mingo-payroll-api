using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<int> AddAsync(PasswordResetToken entity, CancellationToken cancellationToken = default);
    Task<bool> MarkUsedAsync(int id, DateTime usedAt, CancellationToken cancellationToken = default);
    Task InvalidateActiveTokensForUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<int> DeleteExpiredAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
