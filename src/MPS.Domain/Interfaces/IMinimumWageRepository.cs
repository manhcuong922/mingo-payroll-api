using MPS.Domain.Entities.Compliance;

namespace MPS.Domain.Interfaces;

public interface IMinimumWageRepository
{
    Task<MinimumWageVersion?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<MinimumWageVersion>> GetAllAsync(bool includeArchived, CancellationToken ct);

    /// <summary>
    /// Trả về version active tại date, bao gồm đủ 4 Regions (I/II/III/IV).
    /// </summary>
    Task<MinimumWageVersion?> GetActiveAtAsync(DateOnly date, CancellationToken ct);

    /// <summary>
    /// Thêm version mới (kèm 4 MinimumWageRegions) trong transaction Serializable.
    /// Auto-close record đang open hiện tại.
    /// </summary>
    Task<int> AddAsync(MinimumWageVersion entity, CancellationToken ct);

    /// <summary>
    /// Chỉ cho phép update record future-dated (ValidFrom > today) và chưa archived.
    /// </summary>
    Task<bool> UpdateAsync(MinimumWageVersion entity, CancellationToken ct);

    /// <summary>
    /// Soft delete: set IsArchived = true. Phục hồi record liền trước thành open nếu có.
    /// </summary>
    Task<bool> ArchiveAsync(int id, CancellationToken ct);
}
