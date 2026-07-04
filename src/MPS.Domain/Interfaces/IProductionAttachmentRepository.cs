using MPS.Domain.Entities;
using MPS.Domain.Enums;

namespace MPS.Domain.Interfaces;

public interface IProductionAttachmentRepository
{
    Task<IReadOnlyList<ProductionAttachment>> GetByOwnerAsync(
        AttachmentOwnerType ownerType, int ownerId, CancellationToken ct);
    Task<ProductionAttachment?> GetByIdAsync(int id, CancellationToken ct);

    // Đếm attachment của 1 owner — dùng check "task bàn giao phải có ≥1 bằng chứng khi Complete".
    Task<int> CountByOwnerAsync(AttachmentOwnerType ownerType, int ownerId, CancellationToken ct);

    Task<int> AddAsync(ProductionAttachment entity, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
