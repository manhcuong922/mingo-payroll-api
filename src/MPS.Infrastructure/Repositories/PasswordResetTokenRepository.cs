using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly AppDbContext _context;

    public PasswordResetTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _context.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<int> AddAsync(PasswordResetToken entity, CancellationToken cancellationToken = default)
    {
        await _context.PasswordResetTokens.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> MarkUsedAsync(int id, DateTime usedAt, CancellationToken cancellationToken = default)
    {
        var existing = await _context.PasswordResetTokens.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (existing is null) return false;

        existing.UsedAt = usedAt;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task InvalidateActiveTokensForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var active = await _context.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        if (active.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var token in active)
            token.UsedAt = now;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteExpiredAsync(DateTime cutoff, CancellationToken cancellationToken = default)
    {
        var expired = await _context.PasswordResetTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0) return 0;

        _context.PasswordResetTokens.RemoveRange(expired);
        await _context.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }
}
