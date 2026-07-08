using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class CommentRepository : ICommentRepository
{
    private readonly AppDbContext _context;

    public CommentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Comment>> GetByOwnerAsync(
        CommentOwnerType ownerType, int ownerId, CancellationToken ct) =>
        await _context.Comments
            .AsNoTracking()
            .Include(c => c.AuthorUser)
            .Where(c => c.OwnerType == ownerType && c.OwnerId == ownerId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<int> AddAsync(Comment entity, CancellationToken ct)
    {
        _context.Comments.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }
}
