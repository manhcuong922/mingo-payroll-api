using MPS.Domain.Entities;
using MPS.Domain.Enums;

namespace MPS.Domain.Interfaces;

public interface ICommentRepository
{
    Task<IReadOnlyList<Comment>> GetByOwnerAsync(CommentOwnerType ownerType, int ownerId, CancellationToken ct);
    Task<int> AddAsync(Comment entity, CancellationToken ct);
}
