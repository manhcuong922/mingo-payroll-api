using MPS.Domain.Entities.Compliance;

namespace MPS.Domain.Interfaces;

public interface IOtRateRepository
{
    Task<OtRateVersion?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<OtRateVersion>> GetAllAsync(bool includeArchived, CancellationToken ct);
    Task<OtRateVersion?> GetActiveAtAsync(DateOnly date, CancellationToken ct);

    /// <summary>
    /// Thêm version mới trong transaction Serializable. Auto-close record đang open hiện tại.
    /// </summary>
    Task<int> AddAsync(OtRateVersion entity, CancellationToken ct);

    /// <summary>
    /// Chỉ cho phép update record future-dated (ValidFrom > today) và chưa archived.
    /// </summary>
    Task<bool> UpdateAsync(OtRateVersion entity, CancellationToken ct);

    /// <summary>
    /// Soft delete: set IsArchived = true. Phục hồi record liền trước thành open nếu có.
    /// </summary>
    Task<bool> ArchiveAsync(int id, CancellationToken ct);
}
