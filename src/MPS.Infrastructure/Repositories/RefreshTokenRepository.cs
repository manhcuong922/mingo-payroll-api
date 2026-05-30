using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _context.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveTokensByUserAsync(int userId, CancellationToken cancellationToken = default) =>
        await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

    public async Task<int> AddAsync(RefreshToken entity, CancellationToken cancellationToken = default)
    {
        await _context.RefreshTokens.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(RefreshToken entity, CancellationToken cancellationToken = default)
    {
        var existing = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Id == entity.Id, cancellationToken);
        if (existing is null) return false;

        existing.RevokedAt = entity.RevokedAt;
        existing.RevokedReason = entity.RevokedReason;
        existing.ReplacedByTokenHash = entity.ReplacedByTokenHash;
        existing.RevokedByIp = entity.RevokedByIp;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RevokeAllForUserAsync(int userId, string reason, string? revokedByIp, CancellationToken cancellationToken = default)
    {
        var active = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var token in active)
        {
            token.RevokedAt = now;
            token.RevokedReason = reason;
            token.RevokedByIp = revokedByIp;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
