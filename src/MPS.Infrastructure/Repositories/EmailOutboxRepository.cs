using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class EmailOutboxRepository : IEmailOutboxRepository
{
    private readonly AppDbContext _context;

    public EmailOutboxRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> AddAsync(EmailOutboxMessage message, CancellationToken cancellationToken = default)
    {
        await _context.EmailOutboxMessages.AddAsync(message, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return message.Id;
    }

    public async Task<IReadOnlyList<EmailOutboxMessage>> GetPendingBatchAsync(
        int batchSize, DateTime now, CancellationToken cancellationToken = default) =>
        await _context.EmailOutboxMessages
            .Where(e => e.Status == EmailOutboxStatus.Pending
                        && (e.NextAttemptAt == null || e.NextAttemptAt <= now))
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    public async Task<int> DeleteSentBeforeAsync(DateTime cutoff, CancellationToken cancellationToken = default)
    {
        var stale = await _context.EmailOutboxMessages
            .Where(e => e.Status == EmailOutboxStatus.Sent && e.SentAt != null && e.SentAt < cutoff)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0) return 0;

        _context.EmailOutboxMessages.RemoveRange(stale);
        await _context.SaveChangesAsync(cancellationToken);
        return stale.Count;
    }
}
