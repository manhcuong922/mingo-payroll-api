using MPS.Domain.Entities.Compliance;

namespace MPS.Domain.Interfaces;

public interface IPitRegulationRepository
{
    Task<PitRegulationVersion?> GetByIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<PitRegulationVersion>> GetAllAsync(bool includeArchived, CancellationToken ct);

    /// <summary>
    /// Trả về version active tại date, bao gồm Brackets đã sắp xếp theo Order asc.
    /// </summary>
    Task<PitRegulationVersion?> GetActiveAtAsync(DateOnly date, CancellationToken ct);

    /// <summary>
    /// Thêm version mới (kèm PitBrackets) trong transaction Serializable.
    /// Auto-close record đang open hiện tại.
    /// </summary>
    Task<int> AddAsync(PitRegulationVersion entity, CancellationToken ct);

    /// <summary>
    /// Chỉ cho phép update record future-dated (ValidFrom > today) và chưa archived.
    /// </summary>
    Task<bool> UpdateAsync(PitRegulationVersion entity, CancellationToken ct);

    /// <summary>
    /// Soft delete: set IsArchived = true. Phục hồi record liền trước thành open nếu có.
    /// </summary>
    Task<bool> ArchiveAsync(int id, CancellationToken ct);
}
