using Microsoft.EntityFrameworkCore;
using MPS.Domain.Common;
using MPS.Domain.Entities.Notifications;
using MPS.Domain.Interfaces.Notifications;

namespace MPS.Infrastructure.Notifications.Persistence;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _context;

    public NotificationRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken ct = default)
    {
        var now = VietnamClock.Now;
        var list = notifications.ToList();
        foreach (var n in list)
        {
            if (n.Id == Guid.Empty) n.Id = Guid.NewGuid();
            n.CreatedAt = now;
        }

        await _context.Notifications.AddRangeAsync(list, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<Notification> Items, int Total)> GetPagedByRecipientAsync(
        int recipientUserId,
        bool? isRead,
        int page,
        int size,
        CancellationToken ct = default)
    {
        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == recipientUserId);

        if (isRead.HasValue)
            query = query.Where(n => n.IsRead == isRead.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<int> CountUnreadAsync(int recipientUserId, CancellationToken ct = default) =>
        _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.RecipientUserId == recipientUserId && !n.IsRead, ct);

    public async Task<bool> MarkReadAsync(Guid id, int recipientUserId, CancellationToken ct = default)
    {
        var entity = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.RecipientUserId == recipientUserId, ct);

        if (entity is null) return false;
        if (entity.IsRead) return true;

        entity.IsRead = true;
        entity.ReadAt = VietnamClock.Now;
        entity.UpdatedAt = entity.ReadAt;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> MarkAllReadAsync(int recipientUserId, CancellationToken ct = default)
    {
        var now = VietnamClock.Now;
        // ExecuteUpdate (EF Core 8) — cập nhật thẳng DB, không tải entity, hiệu quả với lượng lớn.
        return await _context.Notifications
            .Where(n => n.RecipientUserId == recipientUserId && !n.IsRead)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, now)
                    .SetProperty(n => n.UpdatedAt, now),
                ct);
    }
}
